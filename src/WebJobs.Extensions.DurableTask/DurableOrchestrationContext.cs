﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Serializing;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Parameter data for orchestration bindings that can be used to schedule function-based activities.
    /// </summary>
    public sealed class DurableOrchestrationContext
    {
        private const string DefaultVersion = "";
        private const int MaxTimerDurationInDays = 6;

        // The default JsonDataConverter for DTFx includes type information in JSON objects. This blows up when using Functions 
        // because the type information generated from C# scripts cannot be understood by DTFx. For this reason, explicitly
        // configure the JsonDataConverter with default serializer settings, which don't include CLR type information.
        internal static readonly JsonDataConverter SharedJsonConverter = new JsonDataConverter(new JsonSerializerSettings());

        private readonly Dictionary<string, object> pendingExternalEvents = 
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly DurableTaskExtension config;
        private readonly string orchestrationName;
        private readonly string orchestrationVersion;

        private OrchestrationContext innerContext;
        private string serializedInput;
        private string serializedOutput;
        private int owningThreadId;

        internal DurableOrchestrationContext(
            DurableTaskExtension config,
            string functionName,
            string functionVersion)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));

            this.orchestrationName = functionName;
            this.orchestrationVersion = functionVersion;
            this.owningThreadId = -1;
        }

        /// <summary>
        /// Gets the instance ID of the currently executing orchestration.
        /// </summary>
        /// <remarks>
        /// The instance ID is generated and fixed when the orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The ID of the current orchestration instance.
        /// </value>
        public string InstanceId => this.innerContext.OrchestrationInstance.InstanceId;

        /// <summary>
        /// Gets the current date/time in a way that is safe for use by orchestrator functions.
        /// </summary>
        /// <remarks>
        /// This date/time value is derived from the orchestration history. It always returns the same value 
        /// at specific points in the orchestrator function code, making it deterministic and safe for replay.
        /// </remarks>
        /// <value>The orchestration's current date/time in UTC.</value>
        public DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

        /// <summary>
        /// Gets a value indicating whether the orchestrator function is currently replaying itself.
        /// </summary>
        /// <remarks>
        /// This property is useful when there is logic that needs to run only when the orchestrator function
        /// is *not* replaying. For example, certain types of application logging may become too noisy when duplicated
        /// as part of orchestrator function replay. The orchestrator code could check to see whether the function is
        /// being replayed and then issue the log statements when this value is <c>false</c>.
        /// </remarks>
        /// <value>
        /// <c>true</c> if the orchestrator function is currently being replayed; otherwise <c>false</c>.
        /// </value>
        public bool IsReplaying => this.innerContext.IsReplaying;

        internal bool ContinuedAsNew { get; private set; }

        internal bool IsCompleted { get; set; }

        internal string HubName => this.config.HubName;

        internal string Name => this.orchestrationName;

        internal string Version => this.orchestrationVersion;

        internal bool IsOutputSet => this.serializedOutput != null;

        internal void AssignToCurrentThread()
        {
            this.owningThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Returns the orchestrator function input as a raw JSON string value.
        /// </summary>
        /// <returns>
        /// The raw JSON-formatted orchestrator function input.
        /// </returns>
        public string GetRawInput()
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
        public JToken GetInputAsJson()
        {
            this.ThrowIfInvalidAccess();
            return this.serializedInput != null ? JToken.Parse(this.serializedInput) : null;
        }

        /// <summary>
        /// Gets the input of the current orchestrator function as a deserialized value.
        /// </summary>
        /// <typeparam name="T">Any data contract type that matches the JSON input.</typeparam>
        /// <returns>The deserialized input value.</returns>
        public T GetInput<T>()
        {
            this.ThrowIfInvalidAccess();

            // Nulls need special handling because the JSON converter will throw
            // if you try to convert a JSON null into a CLR value type.
            if (this.serializedInput == null || this.serializedInput == "null")
            {
                return default(T);
            }

            return SharedJsonConverter.Deserialize<T>(this.serializedInput);
        }

        internal void SetInput(OrchestrationContext frameworkContext, string rawInput)
        {
            this.innerContext = frameworkContext;
            this.serializedInput = rawInput;
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
                    this.serializedOutput = SharedJsonConverter.Serialize(output);
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

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <param name="functionName">The name of the activity function to call.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public Task CallActivityAsync(string functionName, params object[] parameters)
        {
            return this.CallActivityAsync<string>(functionName, parameters);
        }

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <param name="functionName">The name of the activity function to call.</param>
        /// <param name="retryOptions">The retry option for the activity function.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public Task CallActivityWithRetryAsync(string functionName, RetryOptions retryOptions, params object[] parameters)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));    
            }

            return this.CallActivityWithRetryAsync<string>(functionName, retryOptions, parameters);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public Task CallSubOrchestratorAsync(string functionName, params object[] parameters)
        {
            return this.CallSubOrchestratorAsync<string>(functionName, parameters);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="retryOptions">The retry option for the orchestrator function.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public Task CallSubOrchestratorWithRetryAsync(string functionName, RetryOptions retryOptions, params object[] parameters)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallSubOrchestratorWithRetryAsync<string>(functionName, retryOptions, parameters);
        }

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled activity function.</typeparam>
        /// <param name="functionName">The name of the activity function to call.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public async Task<TResult> CallActivityAsync<TResult>(string functionName, params object[] parameters)
        {
            return await CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, null, parameters);
        }

        /// <summary>
        /// Schedules an activity function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled activity function.</typeparam>
        /// <param name="functionName">The name of the activity function to call.</param>
        /// <param name="retryOptions">The retry option for the activity function.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public async Task<TResult> CallActivityWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, params object[] parameters)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return await CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Activity, retryOptions, parameters);
        }

        /// <summary>
        /// Schedules an orchestration function named <paramref name="functionName"/> for execution.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public async Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, params object[] parameters)
        {
            return await CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, null, parameters);
        }

        /// <summary>
        /// Schedules an orchestrator function named <paramref name="functionName"/> for execution with retry options.
        /// </summary>
        /// <typeparam name="TResult">The return type of the scheduled orchestrator function.</typeparam>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="retryOptions">The retry option for the orchestrator function.</param>
        /// <param name="parameters">The JSON-serializeable parameters to pass as input to the function.</param>
        /// <returns>A durable task that completes when the called function completes or fails.</returns>
        /// <exception cref="ArgumentNullException">
        /// The retry option object is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The specified function does not exist, is disabled, or is not an orchestrator function.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The current thread is different than the thread which started the orchestrator execution.
        /// </exception>
        /// <exception cref="DurableTask.Core.Exceptions.TaskFailedException">
        /// The activity function failed with an unhandled exception.
        /// </exception>
        public async Task<TResult> CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, params object[] parameters)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return await CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, retryOptions, parameters);
        }

        /// <summary>
        /// Creates a durable timer which expires at a specified time.
        /// </summary>
        /// <remarks>
        /// All durable timers created using this method must either expire or be cancelled 
        /// using the <paramref name="cancelToken"/> before the orchestrator function completes.
        /// Otherwise the underlying framework will keep the instance alive until the timer expires.
        /// </remarks>
        /// <param name="fireAt">The time at which the timer should expire.</param>
        /// <param name="cancelToken">The <c>CancellationToken</c> to use for cancelling the timer.</param>
        /// <returns>A durable task that completes when the durable timer expires.</returns>
        public Task CreateTimer(DateTime fireAt, CancellationToken cancelToken)
        {
            return this.CreateTimer<string>(fireAt, null, cancelToken);
        }

        /// <summary>
        /// Creates a durable timer which expires at a specified time.
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
        public Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
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
                this.config.HubName,
                this.orchestrationName,
                this.orchestrationVersion,
                this.InstanceId,
                reason: $"CreateTimer:{fireAt:o}",
                isReplay: this.innerContext.IsReplaying);

            return timerTask;
        }

        /// <summary>
        /// Waits asynchronously for an event to be raised with name <paramref name="name"/> and returns the event data.
        /// </summary>
        /// <param name="name">The name of the event to wait for.</param>
        /// <typeparam name="T">Any serializeable type that represents the JSON event payload.</typeparam>
        /// <returns>A durable task that completes when the external event is received.</returns>
        public Task<T> WaitForExternalEvent<T>(string name)
        {
            this.ThrowIfInvalidAccess();

            lock (this.pendingExternalEvents)
            {
                object tcsRef;
                TaskCompletionSource<T> tcs;
                if (!this.pendingExternalEvents.TryGetValue(name, out tcsRef) || (tcs = tcsRef as TaskCompletionSource<T>) == null)
                {
                    tcs = new TaskCompletionSource<T>();
                    this.pendingExternalEvents[name] = tcs;
                }

                this.config.TraceHelper.FunctionListening(
                    this.config.HubName,
                    this.orchestrationName,
                    this.orchestrationVersion,
                    this.InstanceId,
                    reason: $"WaitForExternalEvent:{name}",
                    isReplay: this.innerContext.IsReplaying);

                return tcs.Task;
            }
        }

        /// <summary>
        /// Restarts the orchestration and its history.
        /// </summary>
        /// <param name="input">The JSON-serializeable data to re-initialize the instance with.</param>
        public void ContinueAsNew(object input)
        {
            this.ThrowIfInvalidAccess();

            this.innerContext.ContinueAsNew(input);
            this.ContinuedAsNew = true;
        }

        private async Task<TResult> CallDurableTaskFunctionAsync<TResult>(string functionName,
            FunctionType functionType,
            RetryOptions retryOptions,
            object[] parameters)
        {
            this.ThrowIfInvalidAccess();

            // TODO: Support for versioning
            string version = DefaultVersion;
            this.config.ThrowIfInvalidFunctionType(functionName, functionType, version);

            Task<TResult> callTask;

            switch (functionType)
            {
                case FunctionType.Activity:
                    if (retryOptions == null)
                    {
                        callTask = this.innerContext.ScheduleTask<TResult>(functionName, version, parameters);
                    }
                    else
                    {
                        callTask = this.innerContext.ScheduleWithRetry<TResult>(functionName, version,
                            retryOptions.GetRetryOptions(), parameters);
                    }
                    break;
                case FunctionType.Orchestrator:
                    if (retryOptions == null)
                    {
                        callTask = this.innerContext.CreateSubOrchestrationInstance<TResult>(functionName, version,
                            JsonConvert.SerializeObject(parameters));
                    }
                    else
                    {
                        callTask = this.innerContext.CreateSubOrchestrationInstanceWithRetry<TResult>(functionName, version,
                            retryOptions.GetRetryOptions(), JsonConvert.SerializeObject(parameters));
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        string.Format("Unexpected function type '{0}'.",
                            functionType));
            }

            string sourceFunctionId = string.IsNullOrEmpty(this.orchestrationVersion)
                ? this.orchestrationName
                : this.orchestrationName + "/" + this.orchestrationVersion;

            this.config.TraceHelper.FunctionScheduled(
                this.config.HubName,
                functionName,
                version,
                this.InstanceId,
                reason: sourceFunctionId,
                functionType: functionType,
                isReplay: this.innerContext.IsReplaying);

            TResult output;

            try
            {
                output = await callTask;
            }
            catch (Exception e)
            {
                if (this.innerContext.IsReplaying)
                {
                    // If this were not a replay, then the activity function trigger would have already 
                    // emitted a FunctionFailed trace with the full exception details.
                    this.config.TraceHelper.FunctionFailed(
                        this.config.HubName,
                        functionName,
                        version,
                        this.InstanceId,
                        reason: $"(replayed {e.GetType().Name})",
                        functionType: FunctionType.Activity,
                        isReplay: true);
                }
                throw;
            }

            if (this.innerContext.IsReplaying)
            {
                // If this were not a replay, then the activity function trigger would have already 
                // emitted a FunctionCompleted trace with the actual output details.
                this.config.TraceHelper.FunctionCompleted(
                    this.config.HubName,
                    functionName,
                    version,
                    this.InstanceId,
                    output: "(replayed)",
                    continuedAsNew: false,
                    functionType: FunctionType.Activity,
                    isReplay: true);
            }

            return output;
        }


        internal void RaiseEvent(string name, string input)
        {
            lock (this.pendingExternalEvents)
            {
                object tcs;
                if (this.pendingExternalEvents.TryGetValue(name, out tcs))
                {
                    Type tcsType = tcs.GetType();
                    Type genericTypeArgument = tcsType.GetGenericArguments()[0];

                    object deserializedObject = SharedJsonConverter.Deserialize(input, genericTypeArgument);
                    MethodInfo trySetResult = tcsType.GetMethod("TrySetResult");
                    trySetResult.Invoke(tcs, new[] { deserializedObject });
                }
            }
        }

        private void ThrowIfInvalidAccess()
        {
            if (this.innerContext == null)
            {
                throw new InvalidOperationException("The inner context has not been initialized.");
            }

            // TODO: This should be considered best effort because it's possible that async work 
            // was scheduled and the CLR decided to run it on the same thread. The only guaranteed 
            // way to detect cross-thread access is to do it in the Durable Task Framework directly.
            if (this.owningThreadId != -1 && this.owningThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException(
                    "Multithreaded execution was detected. This can happen if the orchestrator function previously resumed from an unsupported async callback.");
            }
        }
    }
}
