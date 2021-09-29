// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
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

        // Gets dictionary in the format <TaskID-(<result>, <isExceptionBoolean>)> for each
        // task that has a result.
        public Dictionary<int, (string, bool)> GetTaskStates()
        {
            DurableOrchestrationContext ctx = this.context as DurableOrchestrationContext;
            (var actions, var groupedEvents) = this.GetActionsAndGroupedEvents(ctx.History);
            if (actions is null)
            {
                return new Dictionary<int, (string, bool)>();
            }

            var results = this.GetTaskStates(actions, groupedEvents);
            return results;
        }

        private (AsyncAction[] actions, Dictionary<int, List<HistoryEvent>>) GetActionsAndGroupedEvents(IList<HistoryEvent> history)
        {
            var groupedEvents = new Dictionary<int, List<HistoryEvent>>();
            string actionString = "";
            foreach (var historyEvent in history)
            {
                // Obtain lastest instance of serialized actions
                if (historyEvent.EventType is EventType.OrchestratorCompleted)
                {
                    var orchCompletedEvent = (OrchestratorCompletedEvent)historyEvent;
                    actionString = orchCompletedEvent.ActionString;
                }
                else // group history events by their Action ID
                {
                    // if event corresponds to an API
                    var actionId = historyEvent.ActionId;
                    if (actionId != -1)
                    {
                        // add event to the group
                        if (!groupedEvents.ContainsKey(actionId))
                        {
                            // initialize field
                            groupedEvents.Add(actionId, new List<HistoryEvent>());
                        }

                        groupedEvents[actionId].Add(historyEvent);
                    }
                }
            }

            // obtain actions from OOProcOrchState
            if (actionString.Length == 0)
            {
                return (null, groupedEvents);
            }

            var jsonOrchState = JObject.Parse(actionString);
            var orchState = jsonOrchState.ToObject<OutOfProcOrchestratorState>();
            AsyncAction[] actions = orchState.Actions[0];
            return (actions, groupedEvents);
        }

        // Helper to getsdictionary in the format <TaskID-(<result>, <isExceptionBoolean>)> for each
        // task that has a result. Internal-use only.
        // Think of this as the "Process History" algorithm,
        // or the equivalent to the OOProc SDK's TaskOrchestrationExecutor.
        private (Dictionary<int, (string, bool)>, int) GetTaskStates(AsyncAction[] actions, Dictionary<int, List<HistoryEvent>> groupedEvents, int actionId)
        {
            DurableOrchestrationContext ctx = this.context as DurableOrchestrationContext;
            Dictionary<int, (string, bool)> results = new Dictionary<int, (string, bool)>();
            foreach (var action in actions)
            {
                if (action.ActionType is AsyncActionType.CallActivity)
                {
                    if (groupedEvents.TryGetValue(actionId, out var events))
                    {
                        if (events.Count == 2)
                        {
                            // this assumes CallActivity always succeeds, but
                            // a simple conditional check can be done to differentiate
                            // failures from successes. Simplifying here for hackathon :)

                            // I ended up implementing that logic in the CallActivityWithRetry
                            // case, so please see it there.
                            var resultEvent = (TaskCompletedEvent)events.Last();
                            results.Add(actionId, (resultEvent.Result, false));
                        }
                    }
                }
                else if (action.ActionType is AsyncActionType.CreateTimer)
                {
                    if (groupedEvents.TryGetValue(actionId, out var events))
                    {
                        if (events.Count % 2 == 0)
                        {
                            // this logic should successfully handle long timers :)
                            var resultEvent = (TimerFiredEvent)events.Last();
                            if (resultEvent.FireAt >= action.FireAt)
                            {
                                // I'm unsure what result should be given to
                                // timer-fired events, so entering "TBD." for now.
                                results.Add(actionId, ("TBD.", false));
                            }
                        }
                    }
                }
                else if (action.ActionType is AsyncActionType.CallActivityWithRetry)
                {
                    if (groupedEvents.TryGetValue(actionId, out var events))
                    {
                        var lastNonTimerEvent = events.FindLast(
                            (e) =>
                            {
                                var notCreateTimer = e.EventType != EventType.TimerCreated;
                                var notTimerFired = e.EventType != EventType.TimerFired;
                                return notCreateTimer && notTimerFired;
                            });
                        var numAttempts = action.RetryOptions.MaxNumberOfAttempts;
                        var maxNumEvents = (numAttempts * 2) + ((numAttempts - 1) * 2);

                        // this is the kind of logic we can re-use in the CallActivity case
                        // to differentiate between TaskFailed and TaskCompleted.
                        if (lastNonTimerEvent.EventType is EventType.TaskFailed)
                        {
                            if (events.Count >= maxNumEvents)
                            {
                                var resultEvent = (TaskFailedEvent)lastNonTimerEvent;
                                results.Add(actionId, (resultEvent.Reason, true));
                            }
                        }

                        if (lastNonTimerEvent.EventType is EventType.TaskCompleted)
                        {
                            var resultEvent = (TaskCompletedEvent)lastNonTimerEvent;
                            results.Add(actionId, (resultEvent.Result, false));
                        }
                    }
                }
                else if (action.ActionType is AsyncActionType.CallHttp)
                {
                    if (groupedEvents.TryGetValue(actionId, out var events))
                    {
                        // this allows us to handle the polling pattern, ignore
                        // timer events
                        var lastNonTimerEvent = events.FindLast(
                            (e) =>
                            {
                                var notCreateTimer = e.EventType != EventType.TimerCreated;
                                var notTimerFired = e.EventType != EventType.TimerFired;
                                return notCreateTimer && notTimerFired;
                            });

                        if (lastNonTimerEvent.EventType is EventType.TaskCompleted)
                        {
                            var resultEvent = (TaskCompletedEvent)lastNonTimerEvent;
                            var response = JObject.Parse(resultEvent.Result);
                            if (response.TryGetValue("statusCode", out var status))
                            {
                                // handling polling case
                                if (status.ToString() == "200")
                                {
                                    results.Add(actionId, (resultEvent.Result, false));
                                }
                            }
                        }
                    }
                }
                else if (action.ActionType is AsyncActionType.WhenAll)
                {
                    (var compoundResults, var newActionId) = this.GetTaskStates(action.CompoundActions, groupedEvents, actionId);
                    actionId = newActionId - 1; // compensating for the ever-increasing actionId at end of loop
                    compoundResults.ToList().ForEach(x => results.Add(x.Key, x.Value));
                }

                actionId += 1;
            }

            return (results, actionId);
        }

        private Dictionary<int, (string, bool)> GetTaskStates(AsyncAction[] actions, Dictionary<int, List<HistoryEvent>> groupedEvents)
        {
            (var results, var _) = this.GetTaskStates(actions, groupedEvents, 0);
            return results;
        }

        internal async Task<bool> ScheduleDurableTaskEvents(OrchestrationInvocationResult result)
        {
            var execution = result.Json.ToObject<OutOfProcOrchestratorState>();

            if (execution.CustomStatus != null)
            {
                this.context.SetCustomStatus(execution.CustomStatus);
            }

            // store Actions payload in OrchestrationInstance so that it gets serialized
            // into OrchestrationCompleted event once this replay is over.
            DurableOrchestrationContext ctx = this.context as DurableOrchestrationContext;
            ctx.InnerContext.OrchestrationInstance.Actions = result.Json.ToString();
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
        /// <returns>If the API returns a task, the DF task corresponding to the input action. Else, null.</returns>
        private Task InvokeAPIFromAction(AsyncAction action)
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
                        if (ctx != null)
                        {
                            // ctx.ThrowIfInvalidTimerLengthForStorageProvider(action.FireAt);
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
                    task = Task.WhenAll(action.CompoundActions.Select(x => this.InvokeAPIFromAction(x)));
                    break;
                case AsyncActionType.WhenAny:
                    task = Task.WhenAny(action.CompoundActions.Select(x => this.InvokeAPIFromAction(x)));
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
        /// <returns>An awaitable Task that completes once replay completes.</returns>
        private async Task ProcessAsyncActionsV2(AsyncAction[] actions)
        {
            foreach (AsyncAction action in actions)
            {
                Task t = this.InvokeAPIFromAction(action);
                await Task.WhenAny(t); // roundabout try-catch
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
                case SchemaVersion.V2:
                    // In this schema, action arrays should be 1 dimensional (1 action per yield), but due to legacy behavior they're nested within a 2-dimensional array.
                    if (actions.Length != 1)
                    {
                        throw new ArgumentException($"With OOProc schema {schema}, expected actions array to be of length 1 in outer layer but got size: {actions.Length}");
                    }

                    break;
                case SchemaVersion.Original:
                    await this.ProcessAsyncActionsV1(actions);
                    break;
                default:
                    throw new ArgumentException($"The OOProc schema of of version \"{schema}\" is unsupported by this durable-extension version.");
            }
        }

        private async Task ProcessAsyncActionsV1(AsyncAction[][] actions)
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
                    newTask = this.InvokeAPIFromAction(action);
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
