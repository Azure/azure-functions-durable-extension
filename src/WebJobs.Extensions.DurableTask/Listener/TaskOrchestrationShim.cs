// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
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
    internal class TaskOrchestrationShim : TaskOrchestration
    {
        private readonly DurableTaskExtension config;
        private readonly DurableOrchestrationContext context;

        private Func<Task> functionInvocationCallback;

        public TaskOrchestrationShim(
            DurableTaskExtension config,
            DurableOrchestrationContext context)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
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
        }

        internal DurableOrchestrationContext Context => this.context;

        public void SetFunctionInvocationCallback(Func<Task> callback)
        {
            this.functionInvocationCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public override async Task<string> Execute(OrchestrationContext innerContext, string serializedInput)
        {
            if (this.functionInvocationCallback == null)
            {
                throw new InvalidOperationException($"The {nameof(this.functionInvocationCallback)} has not been assigned!");
            }

            this.context.SetInnerContext(innerContext);
            this.context.SetInput(serializedInput);

            this.config.TraceHelper.FunctionStarting(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                this.config.GetIntputOutputTrace(serializedInput),
                FunctionType.Orchestrator,
                this.context.IsReplaying);

            var orchestratorInfo = this.config.GetOrchestratorInfo(new FunctionName(this.context.Name));

            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.config.LifeCycleNotificationHelper.OrchestratorStartingAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying));
            }

            object returnValue;
            try
            {
                Task invokeTask = this.functionInvocationCallback();
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
                this.config.TraceHelper.FunctionFailed(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    exceptionDetails,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying);

                if (!this.context.IsReplaying)
                {
                    this.context.AddDeferredTask(() => this.config.LifeCycleNotificationHelper.OrchestratorFailedAsync(
                        this.context.HubName,
                        this.context.Name,
                        this.context.InstanceId,
                        exceptionDetails,
                        FunctionType.Orchestrator,
                        this.context.IsReplaying));
                }

                throw new OrchestrationFailureException(
                    $"Orchestrator function '{this.context.Name}' failed: {e.Message}",
                    Utils.SerializeCause(e, MessagePayloadDataConverter.ErrorConverter));
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

            string serializedOutput = this.context.GetSerializedOutput();

            this.config.TraceHelper.FunctionCompleted(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                this.config.GetIntputOutputTrace(serializedOutput),
                this.context.ContinuedAsNew,
                FunctionType.Orchestrator,
                this.context.IsReplaying);
            if (!this.context.IsReplaying)
            {
                this.context.AddDeferredTask(() => this.config.LifeCycleNotificationHelper.OrchestratorCompletedAsync(
                    this.context.HubName,
                    this.context.Name,
                    this.context.InstanceId,
                    this.context.ContinuedAsNew,
                    FunctionType.Orchestrator,
                    this.context.IsReplaying));
            }

            return serializedOutput;
        }

        public override string GetStatus()
        {
            return this.context.GetSerializedCustomStatus();
        }

        public override void RaiseEvent(OrchestrationContext unused, string eventName, string serializedEventData)
        {
            this.config.TraceHelper.ExternalEventRaised(
                this.context.HubName,
                this.context.Name,
                this.context.InstanceId,
                eventName,
                this.config.GetIntputOutputTrace(serializedEventData),
                this.context.IsReplaying);

            this.context.RaiseEvent(eventName, serializedEventData);
        }

        private async Task HandleOutOfProcExecution(JObject executionResult)
        {
            var execution = JsonConvert.DeserializeObject<OutOfProcOrchestratorState>(executionResult.ToString());
            await this.ProcessAsyncActions(execution.Actions);

            if (execution.IsDone)
            {
                this.Context.SetOutput(execution.Output);
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
                            tasks.Add(this.context.CallActivityAsync(action.FunctionName, action.Input));
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
                        case AsyncActionType.CallActivityWithRetry: break;
                        case AsyncActionType.CallSubOrchestrator: break;
                        case AsyncActionType.CallSubOrchestratorWithRetry: break;
                        case AsyncActionType.ContinueAsNew: break;
                        case AsyncActionType.WaitForExternalEvent:
                            tasks.Add(this.context.WaitForExternalEvent<object>(action.ExternalEventName));
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
        }
    }
}
