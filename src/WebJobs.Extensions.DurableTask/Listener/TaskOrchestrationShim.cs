// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Common;
using DurableTask.Core.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Task orchestration implementation which delegates the orchestration implementation to a function.
    /// </summary>
    internal class TaskOrchestrationShim : TaskCommonShim
    {
        private readonly DurableOrchestrationContext context;

        public TaskOrchestrationShim(DurableTaskExtension config, string name)
            : base(config)
        {
            this.context = new DurableOrchestrationContext(config, name);
        }

        private enum AsyncActionType
        {
            CallActivity = 0,
            CallActivityWithRetry = 1,
            CallSubOrchestrator = 2,
            CallSubOrchestratorWithRetry = 3,
            ContinueAsNew = 4,
            CreateTimer = 5,
            WaitForExternalEvent = 6,
            CallEntity = 7,
        }

        public override DurableCommonContext Context => this.context;

        public override RegisteredFunctionInfo GetFunctionInfo()
        {
            FunctionName orchestratorFunction = new FunctionName(this.Context.FunctionName);
            return this.Config.GetOrchestratorInfo(orchestratorFunction);
        }

        public override string GetStatus()
        {
            return this.context.GetSerializedCustomStatus();
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            this.Context.RaiseEvent(eventName, serializedEventData);
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            if (this.FunctionInvocationCallback == null)
            {
                throw new InvalidOperationException($"The {nameof(this.FunctionInvocationCallback)} has not been assigned!");
            }

            this.context.InnerContext = innerContext;
            this.context.RawInput = serializedInput;

            this.Config.TraceHelper.FunctionStarting(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                string.Empty,
                string.Empty,
                this.Config.GetIntputOutputTrace(serializedInput),
                FunctionType.Orchestrator,
                this.context.IsReplaying);

            var orchestratorInfo = this.Config.GetOrchestratorInfo(new FunctionName(this.context.Name));

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.IsReplaying));
            }

            object returnValue;
            try
            {
                Task invokeTask = this.FunctionInvocationCallback();
                if (invokeTask is Task<object> resultTask)
                {
                    // Orchestrator threads cannot perform async I/O, so block on such out-of-proc threads.
                    // Possible performance implications; may need revisiting.
                    returnValue = orchestratorInfo.IsOutOfProc ? resultTask.Result : await resultTask;
                }
                else
                {
                    throw new InvalidOperationException("The WebJobs runtime returned a invocation task that does not support return values!");
                }
            }
            catch (Exception e)
            {
                string exceptionDetails = e.ToString();
                this.Config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    string.Empty,
                    string.Empty,
                    exceptionDetails,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying);

                if (!this.context.IsReplaying)
                {
                    this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        exceptionDetails,
                        this.context.IsReplaying));
                }

                var orchestrationException = new OrchestrationFailureException(
                    $"Orchestrator function '{this.context.Name}' failed: {e.Message}",
                    Utils.SerializeCause(e, MessagePayloadDataConverter.ErrorConverter));

                this.context.OrchestrationException = ExceptionDispatchInfo.Capture(orchestrationException);

                throw orchestrationException;
            }
            finally
            {
                this.context.IsCompleted = true;
            }

            if (returnValue != null)
            {
                if (orchestratorInfo.IsOutOfProc)
                {
                    var jObj = returnValue as JObject;
                    if (jObj != null)
                    {
                        await this.HandleOutOfProcExecution(jObj);
                    }
                    else
                    {
                        throw new ArgumentException("Out of proc orchestrators must return a valid JSON schema.");
                    }
                }
                else
                {
                    this.context.SetOutput(returnValue);
                }
            }

            // release any locks that were held by the orchestration
            // just in case the application code did not do so already
            this.context.ReleaseLocks();

            string serializedOutput = this.context.GetSerializedOutput();

            this.Config.TraceHelper.FunctionCompleted(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                string.Empty,
                string.Empty,
                this.Config.GetIntputOutputTrace(serializedOutput),
                this.context.ContinuedAsNew,
                FunctionType.Orchestrator,
                this.context.IsReplaying);
            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.Config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.ContinuedAsNew,
                    this.context.IsReplaying));
            }

            return serializedOutput;
        }

        private async Task HandleOutOfProcExecution(JObject executionResult)
        {
            var execution = JsonConvert.DeserializeObject<OutOfProcOrchestratorState>(executionResult.ToString());
            if (execution.CustomStatus != null)
            {
                ((IDurableOrchestrationContext)this.context).SetCustomStatus(execution.CustomStatus);
            }

            await this.ProcessAsyncActions(execution.Actions);

            if (!string.IsNullOrEmpty(execution.Error))
            {
                throw new OrchestrationFailureException(
                    $"Orchestrator function '{this.Context.Name}' failed: {execution.Error}");
            }

            if (execution.IsDone)
            {
                this.context.SetOutput(execution.Output);
            }
            else
            {
                // Don't return executions unless the orchestrator has completed.
                await Task.Delay(-1);
            }
        }

        private async Task ProcessAsyncActions(AsyncAction[][] actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException("Out-of-proc orchestrator schema must have a non-null actions property.");
            }

            // Each actionSet represents a particular execution of the orchestration.
            foreach (AsyncAction[] actionSet in actions)
            {
                var tasks = new List<Task>(actions.Length);

                // An actionSet represents all actions that were scheduled within that execution.
                foreach (AsyncAction action in actionSet)
                {
                    switch (action.ActionType)
                    {
                        case AsyncActionType.CallActivity:
                            tasks.Add(this.Context.CallActivityAsync(action.FunctionName, action.Input));
                            break;
                        case AsyncActionType.CreateTimer:
                            using (var cts = new CancellationTokenSource())
                            {
                                tasks.Add(this.context.CreateTimer(action.FireAt, cts.Token));
                                if (action.IsCanceled)
                                {
                                    cts.Cancel();
                                }
                            }

                            break;
                        case AsyncActionType.CallActivityWithRetry:
                            tasks.Add(this.context.CallActivityWithRetryAsync(action.FunctionName, action.RetryOptions, action.Input));
                            break;
                        case AsyncActionType.CallSubOrchestrator:
                            tasks.Add(this.context.CallSubOrchestratorAsync(action.FunctionName, action.InstanceId, action.Input));
                            break;
                        case AsyncActionType.CallSubOrchestratorWithRetry:
                            tasks.Add(this.context.CallSubOrchestratorWithRetryAsync(action.FunctionName, action.RetryOptions, action.InstanceId, action.Input));
                            break;
                        case AsyncActionType.CallEntity:
                            var entityId = EntityId.GetEntityIdFromSchedulerId(action.InstanceId);
                            tasks.Add(((IInterleavingContext)this.context).CallEntityAsync(entityId, action.ExternalEventName, action.Input));
                            break;
                        case AsyncActionType.ContinueAsNew:
                            ((IDurableOrchestrationContext)this.context).ContinueAsNew(action.Input);
                            break;
                        case AsyncActionType.WaitForExternalEvent:
                            tasks.Add(this.Context.WaitForExternalEvent<object>(action.ExternalEventName, "ExternalEvent"));
                            break;
                        default:
                            break;
                    }
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAny(tasks);
                }
            }
        }

        private class OutOfProcOrchestratorState
        {
            [JsonProperty("isDone")]
            internal bool IsDone { get; set; }

            [JsonProperty("actions")]
            internal AsyncAction[][] Actions { get; set; }

            [JsonProperty("output")]
            internal object Output { get; set; }

            [JsonProperty("error")]
            internal string Error { get; set; }

            [JsonProperty("customStatus")]
            internal object CustomStatus { get; set; }
        }

        private class AsyncAction
        {
            [JsonProperty("actionType")]
            [JsonConverter(typeof(StringEnumConverter))]
            internal AsyncActionType ActionType { get; set; }

            [JsonProperty("functionName")]
            internal string FunctionName { get; set; }

            [JsonProperty("input")]
            internal object Input { get; set; }

            [JsonProperty("fireAt")]
            internal DateTime FireAt { get; set; }

            [JsonProperty("externalEventName")]
            internal string ExternalEventName { get; set; }

            [JsonProperty("isCanceled")]
            internal bool IsCanceled { get; set; }

            [JsonProperty("retryOptions")]
            [JsonConverter(typeof(RetryOptionsConverter))]
            internal RetryOptions RetryOptions { get; set; }

            [JsonProperty("instanceId")]
            internal string InstanceId { get; set; }
        }
    }
}
