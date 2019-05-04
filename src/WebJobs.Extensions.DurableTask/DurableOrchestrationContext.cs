// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Parameter data for orchestration bindings that can be used to schedule function-based activities.
    /// </summary>
    public sealed class DurableOrchestrationContext : DurableOrchestrationContextBase
    {
        private const string DefaultVersion = "";
        private const int MaxTimerDurationInDays = 6;

        private readonly Dictionary<string, Stack> pendingExternalEvents =
            new Dictionary<string, Stack>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Queue<string>> bufferedExternalEvents =
            new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly DurableTaskExtension config;
        private readonly string orchestrationName;
        private readonly List<Func<Task>> deferredTasks;

        private OrchestrationContext innerContext;
        private string serializedInput;
        private string serializedOutput;
        private string serializedCustomStatus;
        private int newGuidCounter = 0;

        internal DurableOrchestrationContext(DurableTaskExtension config, string functionName)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.deferredTasks = new List<Func<Task>>();
            this.orchestrationName = functionName;
        }

        /// <inheritdoc />
        public override DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

        /// <inheritdoc />
        public override bool IsReplaying => this.innerContext?.IsReplaying ?? base.IsReplaying;

        internal bool ContinuedAsNew { get; private set; }

        internal bool PreserveUnprocessedEvents { get; private set; }

        internal bool IsOutputSet => this.serializedOutput != null;

        internal bool IsCompleted { get; set; }

        internal ExceptionDispatchInfo OrchestrationException { get; set; }

        internal string HubName => this.config.Options.HubName;

        internal string Name => this.orchestrationName;

        internal IList<HistoryEvent> History { get; set; }

        /// <summary>
        /// Returns the orchestrator function input as a raw JSON string value.
        /// </summary>
        /// <returns>
        /// The raw JSON-formatted orchestrator function input.
        /// </returns>
        internal string GetRawInput()
        {
            this.ThrowIfInvalidAccess();
            return this.serializedInput;
        }

        /// <summary>
        /// Gets the input of the current orchestrator function instance as a <c>JToken</c>.
        /// </summary>
        /// <returns>
        /// The parsed <c>JToken</c> representation of the orchestrator function input.
        /// </returns>
        internal JToken GetInputAsJson()
        {
            return this.serializedInput != null ? JToken.Parse(this.serializedInput) : null;
        }

        /// <inheritdoc />
        public override T GetInput<T>()
        {
            this.ThrowIfInvalidAccess();

            // Nulls need special handling because the JSON converter will throw
            // if you try to convert a JSON null into a CLR value type.
            if (this.serializedInput == null || this.serializedInput == "null")
            {
                return default(T);
            }

            return MessagePayloadDataConverter.Default.Deserialize<T>(this.serializedInput);
        }

        /// <inheritdoc />
        public override Guid NewGuid()
        {
            // The name is a combination of the instance ID, the current orchestrator date/time, and a counter.
            string guidNameValue = string.Concat(
                this.InstanceId,
                "_",
                this.innerContext.CurrentUtcDateTime.ToString("o"),
                "_",
                this.newGuidCounter.ToString());

            this.newGuidCounter++;

            return GuidManager.CreateDeterministicGuid(GuidManager.UrlNamespaceValue, guidNameValue);
        }

        internal void SetInput(string rawInput)
        {
            this.serializedInput = rawInput;
        }

        internal void SetInnerContext(OrchestrationContext frameworkContext)
        {
            this.innerContext = frameworkContext;
        }

        /// <summary>
        /// Sets the JSON-serializeable output of the current orchestrator function.
        /// </summary>
        /// <remarks>
        /// If this method is not called explicitly, the return value of the orchestrator function is used as the output.
        /// </remarks>
        /// <param name="output">The JSON-serializeable value to use as the orchestrator function output.</param>
        internal void SetOutput(object output)
        {
            this.ThrowIfInvalidAccess();

            if (this.IsOutputSet)
            {
                throw new InvalidOperationException("The output has already been set of this orchestration instance.");
            }

            if (output != null)
            {
                JToken json = output as JToken;
                if (json != null)
                {
                    this.serializedOutput = json.ToString(Formatting.None);
                }
                else
                {
                    this.serializedOutput = MessagePayloadDataConverter.Default.Serialize(output);
                }
            }
            else
            {
                this.serializedOutput = null;
            }
        }

        internal string GetSerializedOutput()
        {
            return this.serializedOutput;
        }

        /// <inheritdoc />
        public override void SetCustomStatus(object customStatusObject)
        {
            // Limit the custom status payload to 16 KB
            const int MaxCustomStatusPayloadSizeInKB = 16;
            this.serializedCustomStatus = MessagePayloadDataConverter.Default.Serialize(
                customStatusObject,
                MaxCustomStatusPayloadSizeInKB);
        }

        internal string GetSerializedCustomStatus()
        {
            return this.serializedCustomStatus;
        }

        /// <inheritdoc />
        public override Task<TResult> CallActivityAsync<TResult>(string functionName, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, null, null, input);
        }

        /// <inheritdoc />
        public override Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, null, retryOptions, input);
        }

        /// <inheritdoc />
        public override Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, instanceId, null, input);
        }

        /// <inheritdoc />
        public override Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, instanceId, retryOptions, input);
        }

        /// <summary>
        /// Creates a durable timer that expires at a specified time.
        /// </summary>
        /// <remarks>
        /// All durable timers created using this method must either expire or be cancelled
        /// using the <paramref name="cancelToken"/> before the orchestrator function completes.
        /// Otherwise the underlying framework will keep the instance alive until the timer expires.
        /// </remarks>
        /// <typeparam name="T">The type of <paramref name="state"/>.</typeparam>
        /// <param name="fireAt">The time at which the timer should expire.</param>
        /// <param name="state">Any state to be preserved by the timer.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling the timer.</param>
        /// <returns>A durable task that completes when the durable timer expires.</returns>
        public override async Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
        {
            this.ThrowIfInvalidAccess();

            // This check can be removed once the storage provider supports extended timers.
            // https://github.com/Azure/azure-functions-durable-extension/issues/14
            if (fireAt.Subtract(this.CurrentUtcDateTime) > TimeSpan.FromDays(MaxTimerDurationInDays))
            {
                throw new ArgumentException($"Timer durations must not exceed {MaxTimerDurationInDays} days.", nameof(fireAt));
            }

            Task<T> timerTask = this.innerContext.CreateTimer(fireAt, state, cancelToken);

            this.config.TraceHelper.FunctionListening(
                this.config.Options.HubName,
                this.orchestrationName,
                this.InstanceId,
                reason: $"CreateTimer:{fireAt:o}",
                isReplay: this.innerContext.IsReplaying);

            T result = await timerTask;

            this.config.TraceHelper.TimerExpired(
                this.config.Options.HubName,
                this.orchestrationName,
                this.InstanceId,
                expirationTime: fireAt,
                isReplay: this.innerContext.IsReplaying);

            return result;
        }

        /// <inheritdoc />
        public override Task<T> WaitForExternalEvent<T>(string name)
        {
            this.ThrowIfInvalidAccess();

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
                    this.config.TraceHelper.FunctionListening(
                        this.config.Options.HubName,
                        this.orchestrationName,
                        this.InstanceId,
                        reason: $"WaitForExternalEvent:{name}",
                        isReplay: this.innerContext.IsReplaying);
                }

                return tcs.Task;
            }
        }

        /// <inheritdoc/>
        public override Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout)
        {
            Action<TaskCompletionSource<T>> timedOutAction = cts =>
                cts.TrySetException(new TimeoutException($"Event {name} not received in {timeout}"));
            return this.WaitForExternalEvent(name, timeout, timedOutAction);
        }

        /// <inheritdoc/>
        public override Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue)
        {
            Action<TaskCompletionSource<T>> timedOutAction = cts => cts.TrySetResult(defaultValue);
            return this.WaitForExternalEvent(name, timeout, timedOutAction);
        }

        private Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, Action<TaskCompletionSource<T>> timeoutAction)
        {
            var tcs = new TaskCompletionSource<T>();
            var cts = new CancellationTokenSource();

            var timeoutAt = this.CurrentUtcDateTime + timeout;
            var timeoutTask = this.CreateTimer(timeoutAt, cts.Token);
            var waitForEventTask = this.WaitForExternalEvent<T>(name);

            waitForEventTask.ContinueWith(
                t =>
                {
                    using (cts)
                    {
                        if (t.Exception != null)
                        {
                            tcs.TrySetException(t.Exception);
                        }
                        else
                        {
                            tcs.TrySetResult(t.Result);
                        }

                        cts.Cancel();
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);

            timeoutTask.ContinueWith(
                t =>
                {
                    using (cts)
                    {
                        timeoutAction(tcs);
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }

        /// <inheritdoc />
        public override void ContinueAsNew(object input) => this.ContinueAsNew(input, false);

        /// <inheritdoc />
        public override void ContinueAsNew(object input, bool preserveUnprocessedEvents)
        {
            this.ThrowIfInvalidAccess();

            this.innerContext.ContinueAsNew(input);
            this.ContinuedAsNew = true;
            this.PreserveUnprocessedEvents = preserveUnprocessedEvents;
        }

        private async Task<TResult> CallDurableTaskFunctionAsync<TResult>(
            string functionName,
            FunctionType functionType,
            string instanceId,
            RetryOptions retryOptions,
            object input)
        {
            this.ThrowIfInvalidAccess();

            // TODO: Support for versioning
            string version = DefaultVersion;
            this.config.ThrowIfFunctionDoesNotExist(functionName, functionType);

            Task<TResult> callTask;

            switch (functionType)
            {
                case FunctionType.Activity:
                    System.Diagnostics.Debug.Assert(instanceId == null, "The instanceId parameter should not be used for activity functions.");
                    if (retryOptions == null)
                    {
                        callTask = this.innerContext.ScheduleTask<TResult>(functionName, version, input);
                    }
                    else
                    {
                        callTask = this.innerContext.ScheduleWithRetry<TResult>(
                            functionName,
                            version,
                            retryOptions.GetRetryOptions(),
                            input);
                    }

                    break;
                case FunctionType.Orchestrator:
                    if (retryOptions == null)
                    {
                        callTask = this.innerContext.CreateSubOrchestrationInstance<TResult>(
                            functionName,
                            version,
                            instanceId,
                            input);
                    }
                    else
                    {
                        callTask = this.innerContext.CreateSubOrchestrationInstanceWithRetry<TResult>(
                            functionName,
                            version,
                            instanceId,
                            retryOptions.GetRetryOptions(),
                            input);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unexpected function type '{functionType}'.");
            }

            string sourceFunctionId = this.orchestrationName;

            this.config.TraceHelper.FunctionScheduled(
                this.config.Options.HubName,
                functionName,
                this.InstanceId,
                reason: sourceFunctionId,
                functionType: functionType,
                isReplay: this.innerContext.IsReplaying);

            TResult output;
            Exception exception = null;

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
                if (exception != null && this.innerContext.IsReplaying)
                {
                    // If this were not a replay, then the activity function trigger would have already
                    // emitted a FunctionFailed trace with the full exception details.
                    this.config.TraceHelper.FunctionFailed(
                        this.config.Options.HubName,
                        functionName,
                        this.InstanceId,
                        reason: $"(replayed {exception.GetType().Name})",
                        functionType: functionType,
                        isReplay: true);
                }
            }

            if (this.innerContext.IsReplaying)
            {
                // If this were not a replay, then the activity function trigger would have already
                // emitted a FunctionCompleted trace with the actual output details.
                this.config.TraceHelper.FunctionCompleted(
                    this.config.Options.HubName,
                    functionName,
                    this.InstanceId,
                    output: "(replayed)",
                    continuedAsNew: false,
                    functionType: functionType,
                    isReplay: true);
            }

            return output;
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
                    if (!this.bufferedExternalEvents.TryGetValue(name, out Queue<string> bufferedEvents))
                    {
                        bufferedEvents = new Queue<string>();
                        this.bufferedExternalEvents[name] = bufferedEvents;
                    }

                    bufferedEvents.Enqueue(input);

                    this.config.TraceHelper.ExternalEventSaved(
                        this.HubName,
                        this.Name,
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
                    this.innerContext.SendEvent(instance, eventName, jsonData);
                }
            }
        }

        private void ThrowIfInvalidAccess()
        {
            if (this.innerContext == null)
            {
                throw new InvalidOperationException("The inner context has not been initialized.");
            }

            if (!OrchestrationContext.IsOrchestratorThread)
            {
                throw new InvalidOperationException(
                    "Multithreaded execution was detected. This can happen if the orchestrator function code awaits on a task that was not created by a DurableOrchestrationContext method. More details can be found in this article https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-checkpointing-and-replay#orchestrator-code-constraints.");
            }
        }

        internal void AddDeferredTask(Func<Task> function)
        {
            this.deferredTasks.Add(function);
        }

        internal async Task RunDeferredTasks()
        {
            await Task.WhenAll(this.deferredTasks.Select(x => x()));
            this.deferredTasks.Clear();
        }
    }
}
