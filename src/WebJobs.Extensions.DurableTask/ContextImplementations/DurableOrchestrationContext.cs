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

        private readonly Dictionary<string, IEventTaskCompletionSource> pendingExternalEvents =
            new Dictionary<string, IEventTaskCompletionSource>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Queue<string>> bufferedExternalEvents =
            new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly DurabilityProvider durabilityProvider;
        private readonly int maxActionCount;

        private readonly MessagePayloadDataConverter messageDataConverter;
        private readonly MessagePayloadDataConverter errorDataConverter;

        private int actionCount;

        private string serializedOutput;
        private string serializedCustomStatus;

        private bool isReplaying;

        private int newGuidCounter = 0;

        private LockReleaser lockReleaser = null;

        private MessageSorter messageSorter;

        internal TimeSpan LongRunningTimerIntervalDuration
        {
            get
            {
                return this.durabilityProvider.LongRunningTimerIntervalLength;
            }
        }


        internal TimeSpan MaximumShortTimerDuration
        {
            get
            {
                return this.durabilityProvider.MaximumDelayTime;
            }
        }

        internal DurableOrchestrationContext(DurableTaskExtension config, DurabilityProvider durabilityProvider, string functionName)
            : base(config, functionName)
        {
            this.messageDataConverter = config.MessageDataConverter;
            this.errorDataConverter = config.ErrorDataConverter;
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

        internal bool IsLongRunningTimer { get; private set; }

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
            return MessagePayloadDataConverter.ConvertToJToken(this.RawInput);
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

            return this.messageDataConverter.Deserialize<T>(this.RawInput);
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
                    this.serializedOutput = this.messageDataConverter.Serialize(output);
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
            this.serializedCustomStatus = this.messageDataConverter.Serialize(
                customStatusObject,
                MaxCustomStatusPayloadSizeInKB);
        }

        internal string GetSerializedCustomStatus()
        {
            return this.serializedCustomStatus;
        }

        Task<TResult> IDurableOrchestrationContext.CallSubOrchestratorAsync<TResult>(string functionName, object input)
        {
            return ((IDurableOrchestrationContext)this).CallSubOrchestratorAsync<TResult>(functionName, string.Empty, input);
        }

        /// <inheritdoc />
        Task<TResult> IDurableOrchestrationContext.CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, false, instanceId, null, null, input, null);
        }

        /// <inheritdoc />
        Task<TResult> IDurableOrchestrationContext.CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, false, instanceId, null, retryOptions, input, null);
        }

        Task<DurableHttpResponse> IDurableOrchestrationContext.CallHttpAsync(HttpMethod method, Uri uri, string content, HttpRetryOptions retryOptions)
        {
            DurableHttpRequest req = new DurableHttpRequest(
                method: method,
                uri: uri,
                content: content,
                httpRetryOptions: retryOptions);
            return ((IDurableOrchestrationContext)this).CallHttpAsync(req);
        }

        async Task<DurableHttpResponse> IDurableOrchestrationContext.CallHttpAsync(DurableHttpRequest req)
        {
            DurableHttpResponse durableHttpResponse = await this.ScheduleDurableHttpActivityAsync(req);

            HttpStatusCode currStatusCode = durableHttpResponse.StatusCode;

            while (currStatusCode == HttpStatusCode.Accepted && req.AsynchronousPatternEnabled)
            {
                var headersDictionary = new Dictionary<string, StringValues>(
                        durableHttpResponse.Headers,
                        StringComparer.OrdinalIgnoreCase);

                DateTime fireAt = default(DateTime);
                if (headersDictionary.TryGetValue("Retry-After", out StringValues retryAfter))
                {
                    fireAt = this.InnerContext.CurrentUtcDateTime
                                .AddSeconds(int.Parse(retryAfter));
                }
                else
                {
                    fireAt = this.InnerContext.CurrentUtcDateTime
                                .AddMilliseconds(this.Config.Options.HttpSettings.DefaultAsyncRequestSleepTimeMilliseconds);
                }

                this.IncrementActionsOrThrowException();
                await this.InnerContext.CreateTimer(fireAt, CancellationToken.None);

                DurableHttpRequest durableAsyncHttpRequest = this.CreateLocationPollRequest(
                    req,
                    durableHttpResponse.Headers["Location"]);
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
                retryOptions: req.HttpRetryOptions?.GetRetryOptions(),
                input: req,
                scheduledTimeUtc: null);

            return durableHttpResponse;
        }

        private DurableHttpRequest CreateLocationPollRequest(DurableHttpRequest durableHttpRequest, string locationUri)
        {
            DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
                method: HttpMethod.Get,
                uri: new Uri(locationUri),
                headers: durableHttpRequest.Headers,
                tokenSource: durableHttpRequest.TokenSource,
                timeout: durableHttpRequest.Timeout);

            // Do not copy over the x-functions-key header, as in many cases, the
            // functions key used for the initial request will be a Function-level key
            // and the status endpoint requires a master key.
            newDurableHttpRequest.Headers.Remove("x-functions-key");

            return newDurableHttpRequest;
        }

        /// <inheritdoc />
        async Task<T> IDurableOrchestrationContext.CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
        {
            this.ThrowIfInvalidAccess();

            DateTime intervalFireAt = fireAt;

            if (fireAt.Subtract(this.InnerContext.CurrentUtcDateTime) > this.durabilityProvider.MaximumDelayTime)
            {
                this.IsLongRunningTimer = true;
                intervalFireAt = this.InnerContext.CurrentUtcDateTime.Add(this.durabilityProvider.LongRunningTimerIntervalLength);
            }

            T result = default;

            if (!this.IsLongRunningTimer)
            {
                this.IncrementActionsOrThrowException();
                Task<T> timerTask = this.InnerContext.CreateTimer(fireAt, state, cancelToken);

                this.Config.TraceHelper.FunctionListening(
                    this.Config.Options.HubName,
                    this.OrchestrationName,
                    this.InstanceId,
                    reason: $"CreateTimer:{fireAt:o}",
                    isReplay: this.InnerContext.IsReplaying);

                result = await timerTask;
            }
            else
            {
                this.Config.TraceHelper.FunctionListening(
                    this.Config.Options.HubName,
                    this.OrchestrationName,
                    this.InstanceId,
                    reason: $"CreateTimer:{fireAt:o}",
                    isReplay: this.InnerContext.IsReplaying);

                while (this.InnerContext.CurrentUtcDateTime < fireAt && !cancelToken.IsCancellationRequested)
                {
                    this.IncrementActionsOrThrowException();
                    Task<T> timerTask = this.InnerContext.CreateTimer(intervalFireAt, state, cancelToken);

                    result = await timerTask;

                    TimeSpan remainingTime = fireAt.Subtract(this.InnerContext.CurrentUtcDateTime);

                    if (remainingTime <= TimeSpan.Zero)
                    {
                        break;
                    }
                    else if (remainingTime < this.durabilityProvider.LongRunningTimerIntervalLength)
                    {
                        intervalFireAt = this.InnerContext.CurrentUtcDateTime.Add(remainingTime);
                    }
                    else
                    {
                        intervalFireAt = this.InnerContext.CurrentUtcDateTime.Add(this.durabilityProvider.LongRunningTimerIntervalLength);
                    }
                }
            }

            this.Config.TraceHelper.TimerExpired(
                this.Config.Options.HubName,
                this.OrchestrationName,
                this.InstanceId,
                expirationTime: fireAt,
                isReplay: this.InnerContext.IsReplaying);

            this.IsLongRunningTimer = false;

            return result;
        }

        // We now have built in long-timer support for C#, but in some scenarios, such as out-of-proc,
        // we may still need to enforce this limitations until the solution works there as well.
        internal void ThrowIfInvalidTimerLengthForStorageProvider(DateTime fireAt)
        {
            this.ThrowIfInvalidAccess();

            if (!this.durabilityProvider.ValidateDelayTime(fireAt.Subtract(this.InnerContext.CurrentUtcDateTime), out string errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(fireAt));
            }
        }

        /// <inheritdoc />
        Task<T> IDurableOrchestrationContext.WaitForExternalEvent<T>(string name)
        {
            this.ThrowIfInvalidAccess();
            return this.WaitForExternalEvent<T>(name, "ExternalEvent");
        }

        /// <inheritdoc/>
        Task<T> IDurableOrchestrationContext.WaitForExternalEvent<T>(string name, TimeSpan timeout, CancellationToken cancelToken)
        {
            this.ThrowIfInvalidAccess();
            Action<TaskCompletionSource<T>> timedOutAction = tcs =>
                tcs.TrySetException(new TimeoutException($"Event {name} not received in {timeout}"));
            return this.WaitForExternalEvent(name, timeout, timedOutAction, cancelToken);
        }

        /// <inheritdoc/>
        Task<T> IDurableOrchestrationContext.WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue, CancellationToken cancelToken)
        {
            this.ThrowIfInvalidAccess();
            Action<TaskCompletionSource<T>> timedOutAction = tcs => tcs.TrySetResult(defaultValue);
            return this.WaitForExternalEvent(name, timeout, timedOutAction, cancelToken);
        }

        /// <inheritdoc />
        Task<TResult> IDurableOrchestrationContext.CallActivityAsync<TResult>(string functionName, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, false, null, null, null, input, null);
        }

        /// <inheritdoc />
        Task<TResult> IDurableOrchestrationContext.CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, false, null, null, retryOptions, input, null);
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

            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<object>(entity.EntityName, FunctionType.Entity, true, EntityId.GetSchedulerIdFromEntityId(entity), operationName, null, operationInput, null);
            System.Diagnostics.Debug.Assert(alreadyCompletedTask.IsCompleted, "signaling entities is synchronous");

            try
            {
                alreadyCompletedTask.Wait();
            }
            catch (AggregateException e)
            {
                throw e.InnerException;
            }
        }

        /// <inheritdoc/>
        void IDurableOrchestrationContext.SignalEntity(EntityId entity, DateTime startTime, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<object>(entity.EntityName, FunctionType.Entity, true, EntityId.GetSchedulerIdFromEntityId(entity), operationName, null, operationInput, startTime);
            System.Diagnostics.Debug.Assert(alreadyCompletedTask.IsCompleted, "scheduling operations on entities is synchronous");

            try
            {
                alreadyCompletedTask.Wait();
            }
            catch (AggregateException e)
            {
                throw e.InnerException;
            }
        }

        /// <inheritdoc/>
        string IDurableOrchestrationContext.StartNewOrchestration(string functionName, object input, string instanceId)
        {
            // correlation
#if NETSTANDARD2_0
            var context = CorrelationTraceContext.Current;
#endif
            this.ThrowIfInvalidAccess();
            var actualInstanceId = string.IsNullOrEmpty(instanceId) ? this.NewGuid().ToString() : instanceId;
            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<string>(functionName, FunctionType.Orchestrator, true, actualInstanceId, null, null, input, null);
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
            object input,
            DateTime? scheduledTimeUtc)
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
                        ParentExecutionId = this.ExecutionId,
                        Id = guid,
                        IsSignal = oneWay,
                        Operation = operation,
                        ScheduledTime = scheduledTimeUtc,
                    };
                    if (input != null)
                    {
                        request.SetInput(input, this.messageDataConverter);
                    }

                    this.SendEntityMessage(target, request);

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
                // Check to see if CallHttpAsync() threw a TimeoutException
                // In this case, we want to throw a TimeoutException instead of a FunctionFailedException
                if (functionName.Equals(HttpOptions.HttpTaskActivityReservedName) &&
                    (e.InnerException is TimeoutException || e.InnerException is HttpRequestException))
                {
                    if (e.InnerException is HttpRequestException)
                    {
                        throw new HttpRequestException(e.Message);
                    }

                    throw e.InnerException;
                }

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
            return response.GetResult<TResult>(this.messageDataConverter, this.errorDataConverter);
        }

        internal Task<T> WaitForExternalEvent<T>(string name, string reason)
        {
            lock (this.pendingExternalEvents)
            {
                // We use a stack (a custom implementation using a single-linked list)
                // to make it easier for users to abandon external events
                // that they no longer care about. The common case is a Task.WhenAny in a loop.
                IEventTaskCompletionSource taskCompletionSources;
                EventTaskCompletionSource<T> tcs;

                // Set up the stack for listening to external events
                if (!this.pendingExternalEvents.TryGetValue(name, out taskCompletionSources))
                {
                    tcs = new EventTaskCompletionSource<T>();
                    this.pendingExternalEvents[name] = tcs;
                }
                else
                {
                    if (taskCompletionSources.EventType != typeof(T))
                    {
                        throw new ArgumentException("Events with the same name should have the same type argument.");
                    }
                    else
                    {
                        tcs = new EventTaskCompletionSource<T> { Next = taskCompletionSources };
                        this.pendingExternalEvents[name] = tcs;
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
                IEventTaskCompletionSource taskCompletionSources;
                if (this.pendingExternalEvents.TryGetValue(name, out taskCompletionSources))
                {
                    IEventTaskCompletionSource tcs = taskCompletionSources;

                    // If we're going to raise an event we should remove it from the pending collection
                    // because otherwise WaitForExternalEventAsync() will always find one with this key and run infinitely.
                    IEventTaskCompletionSource next = tcs.Next;
                    if (next == null)
                    {
                        this.pendingExternalEvents.Remove(name);
                    }
                    else
                    {
                        this.pendingExternalEvents[name] = next;
                    }

                    object deserializedObject = this.messageDataConverter.Deserialize(input, tcs.EventType);

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

                    tcs.TrySetResult(deserializedObject);
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
                    JToken jsonData = MessagePayloadDataConverter.ConvertToJToken(rawInput);
                    this.InnerContext.SendEvent(instance, eventName, jsonData);
                }
            }
        }

        private Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, Action<TaskCompletionSource<T>> timeoutAction, CancellationToken cancelToken)
        {
            var tcs = new TaskCompletionSource<T>();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

            var timeoutAt = this.InnerContext.CurrentUtcDateTime + timeout;
            var timeoutTask = ((IDurableOrchestrationContext)this).CreateTimer(timeoutAt, cts.Token);
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
            return this.CallDurableTaskFunctionAsync<TResult>(entityId.EntityName, FunctionType.Entity, false, EntityId.GetSchedulerIdFromEntityId(entityId), operationName, null, operationInput, null);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallEntityAsync(EntityId entityId, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            return this.CallDurableTaskFunctionAsync<object>(entityId.EntityName, FunctionType.Entity, false, EntityId.GetSchedulerIdFromEntityId(entityId), operationName, null, operationInput, null);
        }

        /// <inheritdoc/>
        async Task<IDisposable> IDurableOrchestrationContext.LockAsync(params EntityId[] entities)
        {
            this.ThrowIfInvalidAccess();

            foreach (var entityId in entities)
            {
                this.Config.ThrowIfFunctionDoesNotExist(entityId.EntityName, FunctionType.Entity);
            }

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
                ParentExecutionId = this.ExecutionId,
                LockSet = entities,
                Position = 0,
            };

            this.LockRequestId = lockRequestId.ToString();

            this.SendEntityMessage(target, request);

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
                    this.SendEntityMessage(instance, message);
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

        internal void SendEntityMessage(OrchestrationInstance target, object eventContent)
        {
            string eventName;

            if (eventContent is RequestMessage requestMessage)
            {
                if (requestMessage.ScheduledTime.HasValue)
                {
                    DateTime adjustedDeliveryTime = requestMessage.GetAdjustedDeliveryTime(this.durabilityProvider);
                    eventName = EntityMessageEventNames.ScheduledRequestMessageEventName(adjustedDeliveryTime);
                }
                else
                {
                    this.MessageSorter.LabelOutgoingMessage(
                        requestMessage,
                        target.InstanceId,
                        this.InnerContext.CurrentUtcDateTime,
                        this.Config.MessageReorderWindow);

                    eventName = EntityMessageEventNames.RequestMessageEventName;
                }
            }
            else
            {
                eventName = EntityMessageEventNames.ReleaseMessageEventName;
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

        /// <inheritdoc/>
        Task<TResult> IDurableOrchestrationContext.CallEntityAsync<TResult>(EntityId entityId, string operationName)
        {
            return ((IDurableOrchestrationContext)this).CallEntityAsync<TResult>(entityId, operationName, null);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallEntityAsync(EntityId entityId, string operationName)
        {
            return ((IDurableOrchestrationContext)this).CallEntityAsync<object>(entityId, operationName, null);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallSubOrchestratorAsync(string functionName, object input)
        {
            return ((IDurableOrchestrationContext)this).CallSubOrchestratorAsync<object>(functionName, input);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallSubOrchestratorAsync(string functionName, string instanceId, object input)
        {
            return ((IDurableOrchestrationContext)this).CallSubOrchestratorAsync<object>(functionName, instanceId, input);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallSubOrchestratorWithRetryAsync(string functionName, RetryOptions retryOptions, object input)
        {
            return ((IDurableOrchestrationContext)this).CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, input);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallSubOrchestratorWithRetryAsync(string functionName, RetryOptions retryOptions, string instanceId, object input)
        {
            return ((IDurableOrchestrationContext)this).CallSubOrchestratorWithRetryAsync<object>(functionName, retryOptions, instanceId, input);
        }

        /// <inheritdoc/>
        Task<TResult> IDurableOrchestrationContext.CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, object input)
        {
            return ((IDurableOrchestrationContext)this).CallSubOrchestratorWithRetryAsync<TResult>(functionName, retryOptions, null, input);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CreateTimer(DateTime fireAt, CancellationToken cancelToken)
        {
            return ((IDurableOrchestrationContext)this).CreateTimer<object>(fireAt, null, cancelToken);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.WaitForExternalEvent(string name)
        {
            return ((IDurableOrchestrationContext)this).WaitForExternalEvent<object>(name);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.WaitForExternalEvent(string name, TimeSpan timeout, CancellationToken cancelToken)
        {
            return ((IDurableOrchestrationContext)this).WaitForExternalEvent<object>(name, timeout, cancelToken);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallActivityAsync(string functionName, object input)
        {
            return ((IDurableOrchestrationContext)this).CallActivityAsync<object>(functionName, input);
        }

        /// <inheritdoc/>
        Task IDurableOrchestrationContext.CallActivityWithRetryAsync(string functionName, RetryOptions retryOptions, object input)
        {
            return ((IDurableOrchestrationContext)this).CallActivityWithRetryAsync<object>(functionName, retryOptions, input);
        }

        /// <inheritdoc/>
        TEntityInterface IDurableOrchestrationContext.CreateEntityProxy<TEntityInterface>(string entityKey)
        {
            return ((IDurableOrchestrationContext)this).CreateEntityProxy<TEntityInterface>(new EntityId(DurableEntityProxyHelpers.ResolveEntityName<TEntityInterface>(), entityKey));
        }

        /// <inheritdoc/>
        TEntityInterface IDurableOrchestrationContext.CreateEntityProxy<TEntityInterface>(EntityId entityId)
        {
            return EntityProxyFactory.Create<TEntityInterface>(new OrchestrationContextProxy(this), entityId);
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

        private class EventTaskCompletionSource<T> : TaskCompletionSource<T>, IEventTaskCompletionSource
        {
            public Type EventType => typeof(T);

            public IEventTaskCompletionSource Next { get; set; }

            void IEventTaskCompletionSource.TrySetResult(object result) => this.TrySetResult((T)result);
        }

        /// <summary>
        /// A non-generic tcs interface.
        /// </summary>
#pragma warning disable SA1201 // Elements should appear in the correct order
        private interface IEventTaskCompletionSource
#pragma warning restore SA1201 // Elements should appear in the correct order
        {
            /// <summary>
            /// The type of the event stored in the completion source.
            /// </summary>
            Type EventType { get; }

            /// <summary>
            /// The next task completion source in the stack.
            /// </summary>
            IEventTaskCompletionSource Next { get; set; }

            /// <summary>
            /// Tries to set the result on tcs.
            /// </summary>
            /// <param name="result">The result.</param>
            void TrySetResult(object result);
        }
    }
}
