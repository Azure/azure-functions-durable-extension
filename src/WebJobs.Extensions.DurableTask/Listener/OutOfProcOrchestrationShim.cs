// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Not intended for public consumption.
    /// </summary>
    public class OutOfProcOrchestrationShim
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
        }

        internal async Task HandleOutOfProcExecutionAsync(JObject executionJson)
        {
            bool moreWorkToDo = await this.ExecuteAsync(executionJson);
            if (moreWorkToDo)
            {
                // We must delay indefinitely to prevent the orchestration instance from completing.
                // This is effectively what the Durable Task Framework dispatcher does for normal
                // orchestration execution.
                await Task.Delay(Timeout.Infinite);
            }
        }

        /// <summary>
        /// Not intended for public consumption.
        /// </summary>
        /// <param name="executionJson">The result of the out-of-proc execution.</param>
        /// <returns><c>true</c> if there are more executions to process; <c>false</c> otherwise.</returns>
        public async Task<bool> ExecuteAsync(JObject executionJson)
        {
            var execution = JsonConvert.DeserializeObject<OutOfProcOrchestratorState>(executionJson.ToString());
            if (execution.CustomStatus != null)
            {
                this.context.SetCustomStatus(execution.CustomStatus);
            }

            await this.ProcessAsyncActions(execution.Actions);

            if (!string.IsNullOrEmpty(execution.Error))
            {
                throw new FunctionFailedException(
                    $"Orchestrator function '{this.context.Name}' failed: {execution.Error}");
            }

            if (execution.IsDone)
            {
                this.context.SetOutput(execution.Output);
                return false;
            }

            // there are more executions to process
            return true;
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
                            var entityId = EntityId.GetEntityIdFromSchedulerId(action.EntitySchedulerId);
                            tasks.Add(this.context.CallEntityAsync(entityId, action.EntityOperation, action.Input));
                            break;
                        case AsyncActionType.ContinueAsNew:
                            this.context.ContinueAsNew(action.Input);
                            break;
                        case AsyncActionType.WaitForExternalEvent:
                            tasks.Add(this.context.WaitForExternalEvent<object>(action.ExternalEventName));
                            break;
                        case AsyncActionType.CallHttp:
                            tasks.Add(this.context.CallHttpAsync(action.HttpRequest));
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

            [JsonProperty("httpRequest")]
            internal DurableHttpRequest HttpRequest { get; set; }

            [JsonProperty("entity")]
            internal string EntitySchedulerId { get; set; }

            [JsonProperty("operation")]
            internal string EntityOperation { get; set; }
        }
    }
}
