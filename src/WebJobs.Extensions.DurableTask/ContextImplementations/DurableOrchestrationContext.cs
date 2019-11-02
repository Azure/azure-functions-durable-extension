// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Parameter data for orchestration bindings that can be used to schedule function-based activities.
    /// </summary>
    internal class DurableOrchestrationContext : DurableCommonContext, IDurableOrchestrationContext,
#pragma warning disable 618
        DurableOrchestrationContextBase // for v1 legacy compatibility.
#pragma warning restore 618
    {
        public const string DefaultVersion = "";

        private readonly Dictionary<string, Stack> pendingExternalEvents =
            new Dictionary<string, Stack>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Queue<string>> bufferedExternalEvents =
            new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly DurabilityProvider durabilityProvider;
        private readonly int maxActionCount;

        private int actionCount;

        private string serializedOutput;
        private string serializedCustomStatus;

        private bool isReplaying;

        private int newGuidCounter = 0;

        private LockReleaser lockReleaser = null;

        private MessageSorter messageSorter;

        internal DurableOrchestrationContext(DurableTaskExtension config, DurabilityProvider durabilityProvider, string functionName)
            : base(config, functionName)
        {
            this.durabilityProvider = durabilityProvider;
            this.actionCount = 0;
            this.maxActionCount = config.Options.MaxOrchestrationActions;
        }

        internal string ParentInstanceId { get; set; }

        internal OrchestrationContext InnerContext { get; set; }

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

        internal bool ContinuedAsNew { get; private set; }

        internal bool IsCompleted { get; set; }

        internal ExceptionDispatchInfo OrchestrationException { get; set; }

        internal bool IsOutputSet => this.serializedOutput != null;

        private string OrchestrationName => this.FunctionName;

        internal bool PreserveUnprocessedEvents { get; set; }

        /// <inheritdoc/>
        DateTime IDurableOrchestrationContext.CurrentUtcDateTime => this.InnerContext.CurrentUtcDateTime;

        /// <inheritdoc/>
        bool IDurableOrchestrationContext.IsReplaying => this.InnerContext?.IsReplaying ?? this.IsReplaying;

        /// <inheritdoc />
        string IDurableOrchestrationContext.Name => this.OrchestrationName;

        /// <inheritdoc />
        string IDurableOrchestrationContext.InstanceId => this.InstanceId;

        /// <inheritdoc />
        string IDurableOrchestrationContext.ParentInstanceId => this.ParentInstanceId;

        protected List<EntityId> ContextLocks { get; set; }

        protected string LockRequestId { get; set; }

        private MessageSorter MessageSorter => this.messageSorter ?? (this.messageSorter = new MessageSorter());

        /// <summary>
        /// Returns the orchestrator function input as a raw JSON string value.
        /// </summary>
        /// <returns>
        /// The raw JSON-formatted orchestrator function input.
        /// </returns>
        internal string GetRawInput()
        {
            this.ThrowIfInvalidAccess();
            return this.RawInput;
        }

        /// <summary>
        /// Gets the input of the current orchestrator function instance as a <c>JToken</c>.
        /// </summary>
        /// <returns>
        /// The parsed <c>JToken</c> representation of the orchestrator function input.
        /// </returns>
        internal JToken GetInputAsJson()
        {
            return this.RawInput != null ? JToken.Parse(this.RawInput) : null;
        }

        /// <inheritdoc />
        T IDurableOrchestrationContext.GetInput<T>()
        {
            this.ThrowIfInvalidAccess();

            // Nulls need special handling because the JSON converter will throw
            // if you try to convert a JSON null into a CLR value type.
            if (this.RawInput == null || this.RawInput == "null")
            {
                return default(T);
            }

            return MessagePayloadDataConverter.Default.Deserialize<T>(this.RawInput);
        }

        /// <summary>
        /// Sets the JSON-serializeable output of the current orchestrator function.
        /// </summary>
        /// <remarks>
        /// If this method is not called explicitly, the return value of the orchestrator function is used as the output.
        /// </remarks>
        /// <param name="output">The JSON-serializeable value to use as the orchestrator function output.</param>
        public void SetOutput(object output)
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
        void IDurableOrchestrationContext.SetCustomStatus(object customStatusObject)
        {
            this.ThrowIfInvalidAccess();

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
        Task<TResult> IDurableOrchestrationContext.CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, false, instanceId, null, null, input);
        }

        /// <inheritdoc />
        Task<TResult> IDurableOrchestrationContext.CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, false, instanceId, null, retryOptions, input);
        }

        Task<DurableHttpResponse> IDurableOrchestrationContext.CallHttpAsync(HttpMethod method, Uri uri, string content)
        {
            DurableHttpRequest req = new DurableHttpRequest(
                method: method,
                uri: uri,
                content: content);
            return ((IDurableOrchestrationContext)this).CallHttpAsync(req);
        }

        async Task<DurableHttpResponse> IDurableOrchestrationContext.CallHttpAsync(DurableHttpRequest req)
        {
            DurableHttpResponse durableHttpResponse = await this.ScheduleDurableHttpActivityAsync(req);

            HttpStatusCode currStatusCode = durableHttpResponse.StatusCode;

            while (currStatusCode == HttpStatusCode.Accepted && req.AsynchronousPatternEnabled)
            {
                Dictionary<string, StringValues> headersDictionary = new Dictionary<string, StringValues>(durableHttpResponse.Headers);
                DateTime fireAt = default(DateTime);
                if (headersDictionary.ContainsKey("Retry-After"))
                {
                    fireAt = this.InnerContext.CurrentUtcDateTime.AddSeconds(int.Parse(headersDictionary["Retry-After"]));
                }
                else
                {
                    fireAt = this.InnerContext.CurrentUtcDateTime.AddMilliseconds(this.Config.Options.HttpSettings.DefaultAsyncRequestSleepTimeMilliseconds);
                }

                this.IncrementActionsOrThrowException();
                await this.InnerContext.CreateTimer(fireAt, CancellationToken.None);

                DurableHttpRequest durableAsyncHttpRequest = this.CreateHttpRequestMessageCopy(req, durableHttpResponse.Headers["Location"]);
                durableHttpResponse = await this.ScheduleDurableHttpActivityAsync(durableAsyncHttpRequest);
                currStatusCode = durableHttpResponse.StatusCode;
            }

            return durableHttpResponse;
        }

        private async Task<DurableHttpResponse> ScheduleDurableHttpActivityAsync(DurableHttpRequest req)
        {
            DurableHttpResponse durableHttpResponse = await this.CallDurableTaskFunctionAsync<DurableHttpResponse>(
                functionName: HttpOptions.HttpTaskActivityReservedName,
                functionType: FunctionType.Activity,
                oneWay: false,
                instanceId: null,
                operation: null,
                retryOptions: null,
                input: req);

            return durableHttpResponse;
        }

        private DurableHttpRequest CreateHttpRequestMessageCopy(DurableHttpRequest durableHttpRequest, string locationUri)
        {
            DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
                method: HttpMethod.Get,
                uri: new Uri(locationUri),
                headers: durableHttpRequest.Headers,
                content: durableHttpRequest.Content,
                tokenSource: durableHttpRequest.TokenSource);

            return newDurableHttpRequest;
        }

        /// <inheritdoc />
        async Task<T> IDurableOrchestrationContext.CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
        {
            this.ThrowIfInvalidAccess();

            if (!this.durabilityProvider.ValidateDelayTime(fireAt.Subtract(this.InnerContext.CurrentUtcDateTime), out string errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(fireAt));
            }

            this.IncrementActionsOrThrowException();
            Task<T> timerTask = this.InnerContext.CreateTimer(fireAt, state, cancelToken);

            this.Config.TraceHelper.FunctionListening(
                this.Config.Options.HubName,
                this.OrchestrationName,
                this.InstanceId,
                reason: $"CreateTimer:{fireAt:o}",
                isReplay: this.InnerContext.IsReplaying);

            T result = await timerTask;

            this.Config.TraceHelper.TimerExpired(
                this.Config.Options.HubName,
                this.OrchestrationName,
                this.InstanceId,
                expirationTime: fireAt,
                isReplay: this.InnerContext.IsReplaying);

            return result;
        }

        /// <inheritdoc />
        Task<T> IDurableOrchestrationContext.WaitForExternalEvent<T>(string name)
        {
            this.ThrowIfInvalidAccess();
            return this.WaitForExternalEvent<T>(name, "ExternalEvent");
        }

        /// <inheritdoc/>
        Task<T> IDurableOrchestrationContext.WaitForExternalEvent<T>(string name, TimeSpan timeout)
        {
            this.ThrowIfInvalidAccess();
            Action<TaskCompletionSource<T>> timedOutAction = cts =>
                cts.TrySetException(new TimeoutException($"Event {name} not received in {timeout}"));
            return this.WaitForExternalEvent(name, timeout, timedOutAction);
        }

        /// <inheritdoc/>
        Task<T> IDurableOrchestrationContext.WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue)
        {
            this.ThrowIfInvalidAccess();
            Action<TaskCompletionSource<T>> timedOutAction = cts => cts.TrySetResult(defaultValue);
            return this.WaitForExternalEvent(name, timeout, timedOutAction);
        }

        /// <inheritdoc />
        Task<TResult> IDurableOrchestrationContext.CallActivityAsync<TResult>(string functionName, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, false, null, null, null, input);
        }

        /// <inheritdoc />
        Task<TResult> IDurableOrchestrationContext.CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, false, null, null, retryOptions, input);
        }

        /// <inheritdoc/>
        bool IDurableOrchestrationContext.IsLocked(out IReadOnlyList<EntityId> ownedLocks)
        {
            ownedLocks = this.ContextLocks;
            return ownedLocks != null;
        }

        /// <inheritdoc/>
        Guid IDurableOrchestrationContext.NewGuid()
        {
            return this.NewGuid();
        }

        /// <inheritdoc/>
        void IDurableOrchestrationContext.SignalEntity(EntityId entity, string operationName, object operationInput)
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
        string IDurableOrchestrationContext.StartNewOrchestration(string functionName, object input, string instanceId)
        {
            this.ThrowIfInvalidAccess();
            var actualInstanceId = string.IsNullOrEmpty(instanceId) ? this.NewGuid().ToString() : instanceId;
            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<string>(functionName, FunctionType.Orchestrator, true, actualInstanceId, null, null, input);
            System.Diagnostics.Debug.Assert(alreadyCompletedTask.IsCompleted, "starting orchestrations is synchronous");
            return actualInstanceId;
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

            if (retryOptions != null)
            {
                if (!this.durabilityProvider.ValidateDelayTime(retryOptions.MaxRetryInterval, out string errorMessage))
                {
                    throw new ArgumentException(errorMessage, nameof(retryOptions.MaxRetryInterval));
                }

                if (!this.durabilityProvider.ValidateDelayTime(retryOptions.FirstRetryInterval, out errorMessage))
                {
                    throw new ArgumentException(errorMessage, nameof(retryOptions.FirstRetryInterval));
                }
            }

            // TODO: Support for versioning
            string version = DefaultVersion;
            this.Config.ThrowIfFunctionDoesNotExist(functionName, functionType);

            Task<TResult> callTask = null;
            EntityId? lockToUse = null;
            string operationId = string.Empty;
            string operationName = string.Empty;

            switch (functionType)
            {
                case FunctionType.Activity:
                    System.Diagnostics.Debug.Assert(instanceId == null, "The instanceId parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(operation == null, "The operation parameter should not be used for activity functions.");
                    System.Diagnostics.Debug.Assert(!oneWay, "The oneWay parameter should not be used for activity functions.");
                    if (retryOptions == null)
                    {
                        this.IncrementActionsOrThrowException();
                        callTask = this.InnerContext.ScheduleTask<TResult>(functionName, version, input);
                    }
                    else
                    {
                        this.IncrementActionsOrThrowException();
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
                    if (instanceId != null && instanceId.StartsWith("@"))
                    {
                        throw new ArgumentException(nameof(instanceId), "Orchestration instance ids must not start with @");
                    }

                    if (oneWay)
                    {
                        this.IncrementActionsOrThrowException();
                        var dummyTask = this.InnerContext.CreateSubOrchestrationInstance<TResult>(
                                functionName,
                                version,
                                instanceId,
                                input,
                                new Dictionary<string, string>() { { OrchestrationTags.FireAndForget, "" } });

                        System.Diagnostics.Debug.Assert(dummyTask.IsCompleted, "task should be fire-and-forget");
                    }
                    else
                    {
                        if (this.ContextLocks != null)
                        {
                            throw new LockingRulesViolationException("While holding locks, cannot call suborchestrators.");
                        }

                        if (retryOptions == null)
                        {
                            this.IncrementActionsOrThrowException();
                            callTask = this.InnerContext.CreateSubOrchestrationInstance<TResult>(
                                functionName,
                                version,
                                instanceId,
                                input);
                        }
                        else
                        {
                            this.IncrementActionsOrThrowException();
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

                    if (functionType == FunctionType.Entity)
                    {
                        this.Config.TraceHelper.OperationFailed(
                            this.Config.Options.HubName,
                            functionName,
                            this.InstanceId,
                            operationId,
                            operationName,
                            input: "(replayed)",
                            exception: "(replayed)",
                            duration: 0,
                            isReplay: true);
                    }
                    else
                    {
                        this.Config.TraceHelper.FunctionFailed(
                            this.Config.Options.HubName,
                            functionName,
                            this.InstanceId,
                            reason: $"(replayed {exception.GetType().Name})",
                            functionType: functionType,
                            isReplay: true);
                    }
                }
            }

            if (this.IsReplaying)
            {
                // If this were not a replay, then the orchestrator/activity/entity function trigger would have already
                // emitted a FunctionCompleted trace with the actual output details.

                if (functionType == FunctionType.Entity)
                {
                    this.Config.TraceHelper.OperationCompleted(
                        this.Config.Options.HubName,
                        functionName,
                        this.InstanceId,
                        operationId,
                        operationName,
                        input: "(replayed)",
                        output: "(replayed)",
                        duration: 0,
                        isReplay: true);
                }
                else
                {
                    this.Config.TraceHelper.FunctionCompleted(
                        this.Config.Options.HubName,
                        functionName,
                        this.InstanceId,
                        output: "(replayed)",
                        continuedAsNew: false,
                        functionType: functionType,
                        isReplay: true);
                }
            }

            return output;
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
                            FunctionType.Orchestrator,
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
                        FunctionType.Orchestrator,
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

        private Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, Action<TaskCompletionSource<T>> timeoutAction)
        {
            if (!this.durabilityProvider.ValidateDelayTime(timeout, out string errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(timeout));
            }

            var tcs = new TaskCompletionSource<T>();
            var cts = new CancellationTokenSource();

            var timeoutAt = this.InnerContext.CurrentUtcDateTime + timeout;
            var timeoutTask = this.CreateTimer(timeoutAt, cts.Token);
            var waitForEventTask = this.WaitForExternalEvent<T>(name, "ExternalEvent");

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
                        using (cts)
                        {
                            if (t.Exception == null)
                            {
                                timeoutAction(tcs);
                            }
                            else
                            {
                                // t.Exception is an aggregate exception, so grab internal exception
                                tcs.TrySetException(t.Exception.InnerException);
                            }
                        }
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }

        /// <inheritdoc />
        void IDurableOrchestrationContext.ContinueAsNew(object input, bool preserveUnprocessedEvents)
        {
            this.ThrowIfInvalidAccess();
            this.InnerContext.ContinueAsNew(input);
            this.ContinuedAsNew = true;
            this.PreserveUnprocessedEvents = preserveUnprocessedEvents;
        }

        /// <inheritdoc/>
        Task<TResult> IDurableOrchestrationContext.CallEntityAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            return this.CallDurableTaskFunctionAsync<TResult>(entityId.EntityName, FunctionType.Entity, false, EntityId.GetSchedulerIdFromEntityId(entityId), operationName, null, operationInput);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallEntityAsync(EntityId entityId, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            return this.CallDurableTaskFunctionAsync<object>(entityId.EntityName, FunctionType.Entity, false, EntityId.GetSchedulerIdFromEntityId(entityId), operationName, null, operationInput);
        }

        /// <inheritdoc/>
        async Task<IDisposable> IDurableOrchestrationContext.LockAsync(params EntityId[] entities)
        {
            this.ThrowIfInvalidAccess();
            if (this.ContextLocks != null)
            {
                throw new LockingRulesViolationException("Cannot acquire more locks when already holding some locks.");
            }

            if (entities == null || entities.Length == 0)
            {
                throw new ArgumentException("The list of entities to lock must not be null or empty.", nameof(entities));
            }

            // acquire the locks in a globally fixed order to avoid deadlocks
            Array.Sort(entities);

            // remove duplicates if necessary. Probably quite rare, so no need to optimize more.
            for (int i = 0; i < entities.Length - 1; i++)
            {
                if (entities[i].Equals(entities[i + 1]))
                {
                    entities = entities.Distinct().ToArray();
                    break;
                }
            }

            // use a deterministically replayable unique ID for this lock request, and to receive the response
            var lockRequestId = this.NewGuid();

            // All the entities in entity[] need to be locked, but to avoid deadlock, the locks have to be acquired
            // sequentially, in order. So, we send the lock request to the first entity; when the first lock
            // is granted by the first entity, the first entity will forward the lock request to the second entity,
            // and so on; after the last entity grants the last lock, a response is sent back here.

            // send lock request to first entity in the lock set
            var target = new OrchestrationInstance() { InstanceId = EntityId.GetSchedulerIdFromEntityId(entities[0]) };
            var request = new RequestMessage()
            {
                Id = lockRequestId,
                ParentInstanceId = this.InstanceId,
                LockSet = entities,
                Position = 0,
            };

            this.LockRequestId = lockRequestId.ToString();

            this.SendEntityMessage(target, "op", request);

            // wait for the response from the last entity in the lock set
            await this.WaitForExternalEvent<ResponseMessage>(this.LockRequestId, "LockAcquisitionCompleted");

            this.ContextLocks = new List<EntityId>(entities);

            // return an IDisposable that releases the lock
            this.lockReleaser = new LockReleaser(this);

            return this.lockReleaser;
        }

        public void ReleaseLocks()
        {
            if (this.ContextLocks != null)
            {
                foreach (var entityId in this.ContextLocks)
                {
                    var instance = new OrchestrationInstance() { InstanceId = EntityId.GetSchedulerIdFromEntityId(entityId) };
                    var message = new ReleaseMessage()
                    {
                        ParentInstanceId = this.InstanceId,
                        LockRequestId = this.LockRequestId,
                    };
                    this.SendEntityMessage(instance, "release", message);
                }

                this.ContextLocks = null;
                this.lockReleaser = null;
                this.LockRequestId = null;
            }
        }

        private void ThrowIfInvalidAccess()
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

        internal void SendEntityMessage(OrchestrationInstance target, string eventName, object eventContent)
        {
            if (eventContent is RequestMessage requestMessage)
            {
                this.MessageSorter.LabelOutgoingMessage(
                    requestMessage,
                    target.InstanceId,
                    this.InnerContext.CurrentUtcDateTime,
                    TimeSpan.FromMinutes(this.Config.Options.EntityMessageReorderWindowInMinutes));
            }

            if (!this.IsReplaying)
            {
                this.Config.TraceHelper.SendingEntityMessage(
                    this.InstanceId,
                    this.ExecutionId,
                    target.InstanceId,
                    eventName,
                    eventContent);
            }

            this.IncrementActionsOrThrowException();
            this.InnerContext.SendEvent(target, eventName, eventContent);
        }

        private void IncrementActionsOrThrowException()
        {
            if (this.actionCount >= this.maxActionCount)
            {
                throw new InvalidOperationException("Maximum amount of orchestration actions (" + this.maxActionCount + ") has been reached. This value can be configured in host.json file as MaxOrchestrationActions.");
            }
            else
            {
                this.actionCount++;
            }
        }

        private Guid NewGuid()
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

        private class LockReleaser : IDisposable
        {
            private readonly DurableOrchestrationContext context;

            public LockReleaser(DurableOrchestrationContext context)
            {
                this.context = context;
            }

            public void Dispose()
            {
                if (this.context.lockReleaser == this)
                {
                    this.context.ReleaseLocks();
                }
            }
        }
    }
}
