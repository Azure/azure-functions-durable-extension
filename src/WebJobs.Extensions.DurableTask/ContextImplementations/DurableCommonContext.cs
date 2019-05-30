// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Common functionality used by both <see cref="DurableOrchestrationContext"/>
    /// and <see cref="DurableEntityContext"/>.
    /// </summary>
    internal abstract class DurableCommonContext : IDeterministicExecutionContext
    {
        private const string DefaultVersion = "";

        private readonly Dictionary<string, Stack> pendingExternalEvents =
            new Dictionary<string, Stack>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Queue<string>> bufferedExternalEvents =
            new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly List<Func<Task>> deferredTasks
            = new List<Func<Task>>();

        private bool isReplaying;

        internal DurableCommonContext(DurableTaskExtension config, string functionName)
        {
            this.Config = config ?? throw new ArgumentNullException(nameof(config));
            this.FunctionName = functionName;
        }

        internal DurableTaskExtension Config { get; }

        internal string FunctionName { get; }

        internal abstract FunctionType FunctionType { get; }

        internal bool PreserveUnprocessedEvents { get; set; }

        protected List<EntityId> ContextLocks { get; set; }

        protected string LockRequestId { get; set; }

        internal bool IsReplaying
        {
            get
            {
                return this.InnerContext?.IsReplaying ?? this.isReplaying;
            }

            set
            {
                this.isReplaying = value;
            }
        }

        internal string InstanceId { get; set; }

        internal string ExecutionId { get; set; }

        internal string ParentInstanceId { get; set; }

        internal IList<HistoryEvent> History { get; set; }

        internal string RawInput { get; set; }

        internal bool IsCompleted { get; set; }

        internal OrchestrationContext InnerContext { get; set; }

        internal ExceptionDispatchInfo OrchestrationException { get; set; }

        internal string HubName => this.Config.Options.HubName;

        internal string Name => this.FunctionName;

        /// <inheritdoc/>
        DateTime IDeterministicExecutionContext.CurrentUtcDateTime => this.InnerContext.CurrentUtcDateTime;

        /// <inheritdoc/>
        bool IDeterministicExecutionContext.IsReplaying => this.InnerContext?.IsReplaying ?? this.IsReplaying;

        internal void AddDeferredTask(Func<Task> function)
        {
            this.deferredTasks.Add(function);
        }

        internal async Task RunDeferredTasks()
        {
            await Task.WhenAll(this.deferredTasks.Select(x => x()));
            this.deferredTasks.Clear();
        }

        /// <inheritdoc />
        Task<TResult> IDeterministicExecutionContext.CallActivityAsync<TResult>(string functionName, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, false, null, null, null, input);
        }

        /// <inheritdoc />
        Task<TResult> IDeterministicExecutionContext.CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, false, null, null, retryOptions, input);
        }

        /// <inheritdoc/>
        bool IDeterministicExecutionContext.IsLocked(out IReadOnlyList<EntityId> ownedLocks)
        {
            ownedLocks = this.ContextLocks;
            return ownedLocks != null;
        }

        /// <inheritdoc/>
        Guid IDeterministicExecutionContext.NewGuid()
        {
            return this.NewGuid();
        }

        /// <inheritdoc/>
        void IDeterministicExecutionContext.SignalEntity(EntityId entity, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<object>(entity.EntityName, FunctionType.Entity, true, EntityId.GetSchedulerIdFromEntityId(entity), operationName, null, operationInput);
            System.Diagnostics.Debug.Assert(alreadyCompletedTask.IsCompleted, "signaling entities is synchronous");
            alreadyCompletedTask.Wait(); // just so we see exceptions during testing
        }

        /// <inheritdoc/>
        string IDeterministicExecutionContext.StartNewOrchestration(string functionName, object input, string instanceId)
        {
            this.ThrowIfInvalidAccess();
            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<string>(functionName, FunctionType.Orchestrator, true, instanceId, null, null, input);
            System.Diagnostics.Debug.Assert(alreadyCompletedTask.IsCompleted, "starting orchestrations is synchronous");
            return alreadyCompletedTask.Result;
        }

        internal abstract Guid NewGuid();

        internal virtual void ThrowIfInvalidAccess()
        {
            if (this.InnerContext == null)
            {
                throw new InvalidOperationException("The inner context has not been initialized.");
            }

            if (!OrchestrationContext.IsOrchestratorThread)
            {
                throw new InvalidOperationException(
                    "Multithreaded execution was detected. This can happen if the orchestrator function code awaits on a task that was not created by a DurableOrchestrationContext method. More details can be found in this article https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-checkpointing-and-replay#orchestrator-code-constraints.");
            }
        }

        internal async Task<TResult> CallDurableTaskFunctionAsync<TResult>(
             string functionName,
             FunctionType functionType,
             bool oneWay,
             string instanceId,
             string operation,
             RetryOptions retryOptions,
             object input)
        {
            this.ThrowIfInvalidAccess();

            // TODO: Support for versioning
            string version = DefaultVersion;
            this.Config.ThrowIfFunctionDoesNotExist(functionName, functionType);

            Task<TResult> callTask = null;
            EntityId? lockToUse = null;
            string operationId = string.Empty;
            string operationName = string.Empty;
            bool isEntity = this is DurableEntityContext;

            switch (functionType)
            {
                case FunctionType.Activity:
                    System.Diagnostics.Debug.Assert(instanceId == null, "The instanceId parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(operation == null, "The operation parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(!oneWay, "The oneWay parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(!isEntity, "Entities cannot call activities");
                    if (retryOptions == null)
                    {
                        callTask = this.InnerContext.ScheduleTask<TResult>(functionName, version, input);
                    }
                    else
                    {
                        callTask = this.InnerContext.ScheduleWithRetry<TResult>(
                            functionName,
                            version,
                            retryOptions.GetRetryOptions(),
                            input);
                    }

                    break;

                case FunctionType.Orchestrator:
                    // Instance IDs should not be reused when creating sub-orchestrations. This is a best-effort
                    // check. We cannot easily check the full hierarchy, so we just look at the current orchestration
                    // and the immediate parent.
                    if (string.Equals(instanceId, this.InstanceId, StringComparison.OrdinalIgnoreCase) ||
                        (this.ParentInstanceId != null && string.Equals(instanceId, this.ParentInstanceId, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new ArgumentException("The instance ID of a sub-orchestration must be different than the instance ID of a parent orchestration.");
                    }

                    System.Diagnostics.Debug.Assert(operation == null, "The operation parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(oneWay || !isEntity, "Entities cannot call orchestrations");
                    if (instanceId != null && instanceId.StartsWith("@"))
                    {
                        throw new ArgumentException(nameof(instanceId), "Orchestration instance ids must not start with @");
                    }

                    if (oneWay)
                    {
                        throw new NotImplementedException(); // TODO
                    }
                    else
                    {
                        if (this.ContextLocks != null)
                        {
                            throw new LockingRulesViolationException("While holding locks, cannot call suborchestrators.");
                        }

                        if (retryOptions == null)
                        {
                            callTask = this.InnerContext.CreateSubOrchestrationInstance<TResult>(
                                functionName,
                                version,
                                instanceId,
                                input);
                        }
                        else
                        {
                            callTask = this.InnerContext.CreateSubOrchestrationInstanceWithRetry<TResult>(
                                functionName,
                                version,
                                instanceId,
                                retryOptions.GetRetryOptions(),
                                input);
                        }
                    }

                    break;

                case FunctionType.Entity:
                    System.Diagnostics.Debug.Assert(operation != null, "The operation parameter is required.");
                    System.Diagnostics.Debug.Assert(oneWay || !isEntity, "Entities cannot call entities");
                    System.Diagnostics.Debug.Assert(retryOptions == null, "Retries are not supported for entity calls.");
                    System.Diagnostics.Debug.Assert(instanceId != null, "Entity calls need to specify the target entity.");

                    if (this.ContextLocks != null)
                    {
                        lockToUse = EntityId.GetEntityIdFromSchedulerId(instanceId);
                        if (oneWay)
                        {
                            if (this.ContextLocks.Contains(lockToUse.Value))
                            {
                                throw new LockingRulesViolationException("While holding locks, cannot signal entities whose lock is held.");
                            }
                        }
                        else
                        {
                            if (!this.ContextLocks.Remove(lockToUse.Value))
                            {
                                throw new LockingRulesViolationException("While holding locks, cannot call entities whose lock is not held.");
                            }
                        }
                    }

                    var guid = this.NewGuid(); // deterministically replayable unique id for this request
                    var target = new OrchestrationInstance() { InstanceId = instanceId };
                    operationId = guid.ToString();
                    operationName = operation;
                    var request = new RequestMessage()
                    {
                        ParentInstanceId = this.InstanceId,
                        Id = guid,
                        IsSignal = oneWay,
                        Operation = operation,
                    };
                    if (input != null)
                    {
                        request.SetInput(input);
                    }

                    this.SendEntityMessage(target, "op", request);

                    if (!oneWay)
                    {
                        callTask = this.WaitForEntityResponse<TResult>(guid, lockToUse);
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unexpected function type '{functionType}'.");
            }

            string sourceFunctionId = this.FunctionName;

            this.Config.TraceHelper.FunctionScheduled(
                this.Config.Options.HubName,
                functionName,
                this.InstanceId,
                reason: sourceFunctionId,
                functionType: functionType,
                isReplay: this.IsReplaying);

            TResult output;
            Exception exception = null;

            if (oneWay)
            {
                return default(TResult);
            }

            System.Diagnostics.Debug.Assert(callTask != null, "Two-way operations are asynchronous, so callTask must not be null.");

            try
            {
                output = await callTask;
            }
            catch (TaskFailedException e)
            {
                exception = e;
                string message = string.Format(
                    "The {0} function '{1}' failed: \"{2}\". See the function execution logs for additional details.",
                    functionType.ToString().ToLowerInvariant(),
                    functionName,
                    e.InnerException?.Message);
                throw new FunctionFailedException(message, e.InnerException);
            }
            catch (SubOrchestrationFailedException e)
            {
                exception = e;
                string message = string.Format(
                    "The {0} function '{1}' failed: \"{2}\". See the function execution logs for additional details.",
                    functionType.ToString().ToLowerInvariant(),
                    functionName,
                    e.InnerException?.Message);
                throw new FunctionFailedException(message, e.InnerException);
            }
            catch (Exception e)
            {
                exception = e;
                throw;
            }
            finally
            {
                if (exception != null && this.IsReplaying)
                {
                    // If this were not a replay, then the orchestrator/activity/entity function trigger would have already
                    // emitted a FunctionFailed trace with the full exception details.
                    this.Config.TraceHelper.FunctionFailed(
                        this.Config.Options.HubName,
                        functionName,
                        this.InstanceId,
                        operationId,
                        operationName,
                        reason: $"(replayed {exception.GetType().Name})",
                        functionType: functionType,
                        isReplay: true);
                }
            }

            if (this.IsReplaying)
            {
                // If this were not a replay, then the orchestrator/activity/entity function trigger would have already
                // emitted a FunctionCompleted trace with the actual output details.
                this.Config.TraceHelper.FunctionCompleted(
                    this.Config.Options.HubName,
                    functionName,
                    this.InstanceId,
                    operationId,
                    operationName,
                    output: "(replayed)",
                    continuedAsNew: false,
                    functionType: functionType,
                    isReplay: true);
            }

            return output;
        }

        internal abstract void SendEntityMessage(OrchestrationInstance target, string eventName, object eventContent);

        internal Task<T> WaitForExternalEvent<T>(string name, string reason)
        {
            lock (this.pendingExternalEvents)
            {
                // We use a stack to make it easier for users to abandon external events
                // that they no longer care about. The common case is a Task.WhenAny in a loop.
                Stack taskCompletionSources;
                TaskCompletionSource<T> tcs;

                // Set up the stack for listening to external events
                if (!this.pendingExternalEvents.TryGetValue(name, out taskCompletionSources))
                {
                    tcs = new TaskCompletionSource<T>();
                    taskCompletionSources = new Stack();
                    taskCompletionSources.Push(tcs);
                    this.pendingExternalEvents[name] = taskCompletionSources;
                }
                else
                {
                    if (taskCompletionSources.Count > 0 &&
                        taskCompletionSources.Peek().GetType() != typeof(TaskCompletionSource<T>))
                    {
                        throw new ArgumentException("Events with the same name should have the same type argument.");
                    }
                    else
                    {
                        tcs = new TaskCompletionSource<T>();
                        taskCompletionSources.Push(tcs);
                    }
                }

                // Check the queue to see if any events came in before the orchestrator was listening
                if (this.bufferedExternalEvents.TryGetValue(name, out Queue<string> queue))
                {
                    string rawInput = queue.Dequeue();

                    if (queue.Count == 0)
                    {
                        this.bufferedExternalEvents.Remove(name);
                    }

                    // We can call raise event right away, since we already have an event's input
                    this.RaiseEvent(name, rawInput);
                }
                else
                {
                    this.Config.TraceHelper.FunctionListening(
                        this.Config.Options.HubName,
                        this.FunctionName,
                        this.InstanceId,
                        reason: $"WaitFor{reason}:{name}",
                        isReplay: this.IsReplaying);
                }

                return tcs.Task;
            }
        }

        internal async Task<TResult> WaitForEntityResponse<TResult>(Guid guid, EntityId? lockToUse)
        {
            var response = await this.WaitForExternalEvent<ResponseMessage>(guid.ToString(), "EntityResponse");

            if (lockToUse.HasValue)
            {
                // the lock is available again now that the entity call returned
                this.ContextLocks.Add(lockToUse.Value);
            }

            // can rethrow an exception if that was the result of the operation
            return response.GetResult<TResult>();
        }

        internal void RaiseEvent(string name, string input)
        {
            lock (this.pendingExternalEvents)
            {
                Stack taskCompletionSources;
                if (this.pendingExternalEvents.TryGetValue(name, out taskCompletionSources))
                {
                    object tcs = taskCompletionSources.Pop();
                    Type tcsType = tcs.GetType();
                    Type genericTypeArgument = tcsType.GetGenericArguments()[0];

                    // If we're going to raise an event we should remove it from the pending collection
                    // because otherwise WaitForExternalEventAsync() will always find one with this key and run infinitely.
                    if (taskCompletionSources.Count == 0)
                    {
                        this.pendingExternalEvents.Remove(name);
                    }

                    object deserializedObject = MessagePayloadDataConverter.Default.Deserialize(input, genericTypeArgument);

                    if (deserializedObject is ResponseMessage responseMessage)
                    {
                        this.Config.TraceHelper.EntityResponseReceived(
                            this.HubName,
                            this.Name,
                            this.FunctionType,
                            this.InstanceId,
                            name,
                            this.Config.GetIntputOutputTrace(responseMessage.Result),
                            this.IsReplaying);
                    }
                    else
                    {
                        this.Config.TraceHelper.ExternalEventRaised(
                             this.HubName,
                             this.Name,
                             this.InstanceId,
                             name,
                             this.Config.GetIntputOutputTrace(input),
                             this.IsReplaying);
                    }

                    MethodInfo trySetResult = tcsType.GetMethod("TrySetResult");
                    trySetResult.Invoke(tcs, new[] { deserializedObject });
                }
                else
                {
                    // Add the event to an (in-memory) queue, so we don't drop or lose it
                    if (!this.bufferedExternalEvents.TryGetValue(name, out Queue<string> bufferedEvents))
                    {
                        bufferedEvents = new Queue<string>();
                        this.bufferedExternalEvents[name] = bufferedEvents;
                    }

                    bufferedEvents.Enqueue(input);

                    this.Config.TraceHelper.ExternalEventSaved(
                        this.HubName,
                        this.Name,
                        this.FunctionType,
                        this.InstanceId,
                        name,
                        this.IsReplaying);
                }
            }
        }

        internal void RescheduleBufferedExternalEvents()
        {
            var instance = new OrchestrationInstance { InstanceId = this.InstanceId };

            foreach (var pair in this.bufferedExternalEvents)
            {
                string eventName = pair.Key;
                Queue<string> events = pair.Value;

                while (events.Count > 0)
                {
                    // Need to round-trip serialization since SendEvent always tries to serialize.
                    string rawInput = events.Dequeue();
                    JToken jsonData = JToken.Parse(rawInput);
                    this.InnerContext.SendEvent(instance, eventName, jsonData);
                }
            }
        }
    }
}