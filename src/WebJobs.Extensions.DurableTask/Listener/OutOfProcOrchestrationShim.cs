// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.TaskOrchestrationShim;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Not intended for public consumption.
    /// </summary>
    internal class OutOfProcOrchestrationShim
    {
        private readonly IDurableOrchestrationContext context;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutOfProcOrchestrationShim"/> class.
        /// </summary>
        /// <param name="context">The orchestration execution context.</param>
        public OutOfProcOrchestrationShim(IDurableOrchestrationContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Identifiers for each OOProc Schema Version.
        /// </summary>
        internal enum SchemaVersion
        {
            Original = 0,
            V2 = 1,
            V3 = 2,
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
            CallHttp = 8,
            SignalEntity = 9,
            ScheduledSignalEntity = 10,
            WhenAny = 11,
            WhenAll = 12,
        }

        // Handles replaying the Durable Task APIs that the out-of-proc function scheduled
        // with user code.
        public async Task HandleDurableTaskReplay(OrchestrationInvocationResult executionJson)
        {
            bool moreWorkToDo = await this.ScheduleDurableTaskEvents(executionJson);
            if (moreWorkToDo)
            {
                // We must delay indefinitely to prevent the orchestration instance from completing.
                // This is effectively what the Durable Task Framework dispatcher does for normal
                // orchestration execution.
                await Task.Delay(Timeout.Infinite);
            }
        }

        internal async Task<bool> ScheduleDurableTaskEvents(OrchestrationInvocationResult result)
        {
            var execution = result.Json.ToObject<OutOfProcOrchestratorState>();
            if (execution.CustomStatus != null)
            {
                this.context.SetCustomStatus(execution.CustomStatus);
            }

            await this.ReplayOOProcOrchestration(execution.Actions, execution.SchemaVersion);

            if (!string.IsNullOrEmpty(execution.Error))
            {
                string exceptionDetails = $"Message: {execution.Error}, StackTrace: {result.Exception.StackTrace}";
                throw new OrchestrationFailureException(
                        $"Orchestrator function '{this.context.Name}' failed: {execution.Error}",
                        exceptionDetails);
            }

            if (execution.IsDone)
            {
                this.context.SetOutput(execution.Output);
                return false;
            }

            // there are more executions to process
            return true;
        }

        /// <summary>
        /// Invokes a DF API based on the input action object.
        /// </summary>
        /// <param name="action">An OOProc action object representing a DF task.</param>
        /// <param name="schema">The schema version.</param>
        /// <returns>If the API returns a task, the DF task corresponding to the input action. Else, null.</returns>
        private Task InvokeAPIFromAction(AsyncAction action, SchemaVersion schema)
        {
            Task fireAndForgetTask = Task.CompletedTask;
            Task task = null;
            switch (action.ActionType)
            {
                case AsyncActionType.CallActivity:
                    task = this.context.CallActivityAsync(action.FunctionName, action.Input);
                    break;
                case AsyncActionType.CreateTimer:
                    DurableOrchestrationContext ctx = this.context as DurableOrchestrationContext;
                    using (var cts = new CancellationTokenSource())
                    {
                        if (ctx != null && schema < SchemaVersion.V3)
                        {
                            ctx.ThrowIfInvalidTimerLengthForStorageProvider(action.FireAt);
                        }

                        task = this.context.CreateTimer(action.FireAt, cts.Token);

                        if (action.IsCanceled)
                        {
                            cts.Cancel();
                        }
                    }

                    break;
                case AsyncActionType.CallActivityWithRetry:
                    task = this.context.CallActivityWithRetryAsync(action.FunctionName, action.RetryOptions, action.Input);
                    break;
                case AsyncActionType.CallSubOrchestrator:
                    task = this.context.CallSubOrchestratorAsync(action.FunctionName, action.InstanceId, action.Input);
                    break;
                case AsyncActionType.CallSubOrchestratorWithRetry:
                    task = this.context.CallSubOrchestratorWithRetryAsync(action.FunctionName, action.RetryOptions, action.InstanceId, action.Input);
                    break;
                case AsyncActionType.CallEntity:
                    {
                        var entityId = EntityId.GetEntityIdFromSchedulerId(action.InstanceId);
                        task = this.context.CallEntityAsync(entityId, action.EntityOperation, action.Input);
                        break;
                    }

                case AsyncActionType.SignalEntity:
                    {
                        // We do not add a task because this is 'fire and forget'
                        var entityId = EntityId.GetEntityIdFromSchedulerId(action.InstanceId);
                        this.context.SignalEntity(entityId, action.EntityOperation, action.Input);
                        task = fireAndForgetTask;
                        break;
                    }

                case AsyncActionType.ScheduledSignalEntity:
                    {
                        // We do not add a task because this is 'fire and forget'
                        var entityId = EntityId.GetEntityIdFromSchedulerId(action.InstanceId);
                        this.context.SignalEntity(entityId, action.FireAt, action.EntityOperation, action.Input);
                        task = fireAndForgetTask;
                        break;
                    }

                case AsyncActionType.ContinueAsNew:
                    this.context.ContinueAsNew(action.Input);
                    task = fireAndForgetTask;
                    break;
                case AsyncActionType.WaitForExternalEvent:
                    task = this.context.WaitForExternalEvent<object>(action.ExternalEventName);
                    break;
                case AsyncActionType.CallHttp:
                    task = this.context.CallHttpAsync(action.HttpRequest);
                    break;
                case AsyncActionType.WhenAll:
                    task = Task.WhenAll(action.CompoundActions.Select(x => this.InvokeAPIFromAction(x, schema)));
                    break;
                case AsyncActionType.WhenAny:
                    task = Task.WhenAny(action.CompoundActions.Select(x => this.InvokeAPIFromAction(x, schema)));
                    break;
                default:
                    throw new Exception($"Received an unexpected action type from the out-of-proc function: '${action.ActionType}'.");
            }

            return task;
        }

        /// <summary>
        /// Replays the orchestration execution from an OOProc SDK in .NET.
        /// </summary>
        /// <param name="actions">The OOProc actions payload.</param>
        /// <param name="schema">The schema version.</param>
        /// <returns>An awaitable Task that completes once replay completes.</returns>
        private async Task ProcessAsyncActionsV2(AsyncAction[] actions, SchemaVersion schema)
        {
            foreach (AsyncAction action in actions)
            {
                // This line may throw exceptions (usually to validate that some OOProc operation is valid), which we still want to surface
                Task durableTask = this.InvokeAPIFromAction(action, schema);

                // Before awaiting the task, we wrap it in a try/catch block
                // to protect against possible exceptions thrown by user code.
                // Exception handling and exception throwing is managed in the OOProc SDKs and
                // should not affect / interrupt this replay loop
                try
                {
                    await durableTask;
                }
                catch (Exception)
                {
                    // Silently ignore exceptions thrown by user code
                }
            }
        }

        /// <summary>
        /// Replays the OOProc orchestration based on the actions array. It uses the schema enum to
        /// determine which replay implementation is most appropiate.
        /// </summary>
        /// <param name="actions">The OOProc actions payload.</param>
        /// <param name="schema">The OOProc protocol schema version.</param>
        /// <returns>An awaitable Task that completes once replay completes.</returns>
        private async Task ReplayOOProcOrchestration(AsyncAction[][] actions, SchemaVersion schema)
        {
            switch (schema)
            {
                case SchemaVersion.V3:
                case SchemaVersion.V2:
                    // In this schema, action arrays should be 1 dimensional (1 action per yield), but due to legacy behavior they're nested within a 2-dimensional array.
                    if (actions.Length != 1)
                    {
                        throw new ArgumentException($"With OOProc schema {schema}, expected actions array to be of length 1 in outer layer but got size: {actions.Length}");
                    }

                    await this.ProcessAsyncActionsV2(actions[0], schema);
                    break;
                case SchemaVersion.Original:
                    await this.ProcessAsyncActionsV1(actions, schema);
                    break;
                default:
                    throw new ArgumentException($"The OOProc schema of of version \"{schema}\" is unsupported by this durable-extension version.");
            }
        }

        private async Task ProcessAsyncActionsV1(AsyncAction[][] actions, SchemaVersion schema)
        {
            if (actions == null)
            {
                throw new ArgumentNullException("Out-of-proc orchestrator schema must have a non-null actions property.");
            }

            // Each actionSet represents a particular execution of the orchestration.
            foreach (AsyncAction[] actionSet in actions)
            {
                var tasks = new List<Task>(actions.Length);
                DurableOrchestrationContext ctx = this.context as DurableOrchestrationContext;

                // An actionSet represents all actions that were scheduled within that execution.
                Task newTask;
                foreach (AsyncAction action in actionSet)
                {
                    newTask = this.InvokeAPIFromAction(action, schema);
                    if (newTask != Task.CompletedTask)
                    {
                        tasks.Add(newTask);
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

            [DefaultValue(SchemaVersion.Original)]
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("schemaVersion", DefaultValueHandling = DefaultValueHandling.Populate)]
            internal SchemaVersion SchemaVersion { get; set; }
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

            [JsonProperty("compoundActions")]
            internal AsyncAction[] CompoundActions { get; set; }

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

            [JsonProperty("httpRequest")]
            internal DurableHttpRequest HttpRequest { get; set; }

            [JsonProperty("operation")]
            internal string EntityOperation { get; set; }
        }
    }
}
