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
    /// and <see cref="DurableActorContext"/>.
    /// </summary>
    internal abstract class DurableCommonContext : IDeterministicExecutionContext
    {
        private const string DefaultVersion = "";

        private readonly Dictionary<string, Stack> pendingExternalEvents =
            new Dictionary<string, Stack>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Queue> bufferedExternalEvents =
            new Dictionary<string, Queue>(StringComparer.OrdinalIgnoreCase);

        private readonly List<Func<Task>> deferredTasks
            = new List<Func<Task>>();

        private int newGuidCounter = 0;

        private bool isReplaying;

        internal DurableCommonContext(DurableTaskExtension config, string functionName)
        {
            this.Config = config ?? throw new ArgumentNullException(nameof(config));
            this.FunctionName = functionName;
        }

        internal DurableTaskExtension Config { get; }

        internal string FunctionName { get; }

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
        bool IDeterministicExecutionContext.IsLocked(out IReadOnlyList<string> ownedLocks)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        Guid IDeterministicExecutionContext.NewGuid()
        {
            return this.NewGuid();
        }

        /// <inheritdoc/>
        void IDeterministicExecutionContext.SignalActor(ActorId actor, string operationName, object operationContent)
        {
            this.ThrowIfInvalidAccess();
            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<object>(actor.ActorClass, FunctionType.Actor, true, TaskActorShim.GetSchedulerIdFromActorId(actor), operationName, null, operationContent);
            var ignoredValue = alreadyCompletedTask.Result; // just so we see exceptions during testing
        }

        /// <inheritdoc/>
        string IDeterministicExecutionContext.StartNewOrchestration(string functionName, object input, string instanceId)
        {
            this.ThrowIfInvalidAccess();
            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<string>(functionName, FunctionType.Orchestrator, true, instanceId, null, null, input);
            return alreadyCompletedTask.Result;
        }

        internal Guid NewGuid()
        {
            // The name is a combination of the instance ID, the current orchestrator date/time, and a counter.
            string guidNameValue = string.Concat(
                this.InstanceId,
                "_",
                this.InnerContext.CurrentUtcDateTime.ToString("o"),
                "_",
                this.newGuidCounter.ToString());

            this.newGuidCounter++;

            return GuidManager.CreateDeterministicGuid(GuidManager.UrlNamespaceValue, guidNameValue);
        }

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

            switch (functionType)
            {
                case FunctionType.Activity:
                    System.Diagnostics.Debug.Assert(instanceId == null, "The instanceId parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(operation == null, "The operation parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(!oneWay, "The oneWay parameter should not be used for activity functions.");
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
                    System.Diagnostics.Debug.Assert(operation == null, "The operation parameter should not be used for activity functions.");
                    if (oneWay)
                    {
                        throw new NotImplementedException(); // TODO
                    }
                    else if (retryOptions == null)
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

                    break;

                case FunctionType.Actor:
                    System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(operation), "The operation parameter is required.");
                    System.Diagnostics.Debug.Assert(retryOptions == null, "retries are not supported for actor calls");

                    var guid = this.NewGuid(); // deterministically replayable unique id for this request
                    var target = new OrchestrationInstance() { InstanceId = instanceId };
                    var request = new OperationMessage()
                    {
                        ParentInstanceId = this.InstanceId,
                        Id = guid,
                        IsSignal = oneWay,
                        Operation = operation,
                    };
                    request.SetContent(input);
                    var jrequest = JToken.FromObject(request, MessagePayloadDataConverter.DefaultSerializer);
                    this.InnerContext.SendEvent(target, "op", jrequest);

                    if (!oneWay)
                    {
                        callTask = this.WaitForResponseMessage<TResult>(guid);
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
                isReplay: this.InnerContext.IsReplaying);

            TResult output;
            Exception exception = null;

            if (oneWay)
            {
                return default(TResult);
            }

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
                if (exception != null && this.InnerContext.IsReplaying)
                {
                    // If this were not a replay, then the activity function trigger would have already
                    // emitted a FunctionFailed trace with the full exception details.
                    this.Config.TraceHelper.FunctionFailed(
                        this.Config.Options.HubName,
                        functionName,
                        this.InstanceId,
                        reason: $"(replayed {exception.GetType().Name})",
                        functionType: functionType,
                        isReplay: true);
                }
            }

            if (this.InnerContext.IsReplaying)
            {
                // If this were not a replay, then the activity function trigger would have already
                // emitted a FunctionCompleted trace with the actual output details.
                this.Config.TraceHelper.FunctionCompleted(
                    this.Config.Options.HubName,
                    functionName,
                    this.InstanceId,
                    output: "(replayed)",
                    continuedAsNew: false,
                    functionType: functionType,
                    isReplay: true);
            }

            return output;
        }

        internal Task<T> WaitForExternalEvent<T>(string name)
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
                if (this.bufferedExternalEvents.TryGetValue(name, out Queue queue))
                {
                    object input = queue.Dequeue();

                    if (queue.Count == 0)
                    {
                        this.bufferedExternalEvents.Remove(name);
                    }

                    // We can call raise event right away, since we already have an event's input
                    this.RaiseEvent(name, input.ToString());
                }
                else
                {
                    this.Config.TraceHelper.FunctionListening(
                        this.Config.Options.HubName,
                        this.FunctionName,
                        this.InstanceId,
                        reason: $"WaitForExternalEvent:{name}",
                        isReplay: this.InnerContext.IsReplaying);
                }

                return tcs.Task;
            }
        }

        internal async Task<TResult> WaitForResponseMessage<TResult>(Guid guid)
        {
            var response = await this.WaitForExternalEvent<ResponseMessage>(guid.ToString());
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
                    MethodInfo trySetResult = tcsType.GetMethod("TrySetResult");
                    trySetResult.Invoke(tcs, new[] { deserializedObject });
                }
                else
                {
                    // Add the event to an (in-memory) queue, so we don't drop or lose it
                    if (!this.bufferedExternalEvents.TryGetValue(name, out Queue bufferedEvents))
                    {
                        bufferedEvents = new Queue();
                        this.bufferedExternalEvents[name] = bufferedEvents;
                    }

                    bufferedEvents.Enqueue(input);

                    this.Config.TraceHelper.ExternalEventSaved(
                        this.HubName,
                        this.Name,
                        this.InstanceId,
                        name,
                        this.IsReplaying);
                }
            }
        }
    }
}