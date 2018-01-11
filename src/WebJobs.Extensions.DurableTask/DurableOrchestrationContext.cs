﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
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

        /// <inheritdoc />
        public override string InstanceId => this.innerContext.OrchestrationInstance.InstanceId;

        /// <inheritdoc />
        public override DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

        /// <inheritdoc />
        public override bool IsReplaying => this.innerContext.IsReplaying;

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

        /// <inheritdoc />
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
                this.config.HubName,
                this.orchestrationName,
                this.orchestrationVersion,
                this.InstanceId,
                reason: $"CreateTimer:{fireAt:o}",
                isReplay: this.innerContext.IsReplaying);

            T result = await timerTask;

            this.config.TraceHelper.TimerExpired(
                this.config.HubName,
                this.orchestrationName,
                this.orchestrationVersion,
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

        /// <inheritdoc />
        public override void ContinueAsNew(object input)
        {
            this.ThrowIfInvalidAccess();

            this.innerContext.ContinueAsNew(input);
            this.ContinuedAsNew = true;
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
            this.config.ThrowIfInvalidFunctionType(functionName, functionType, version);

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
            Exception exception = null;

            try
            {
                output = await callTask;
            }
            catch (TaskFailedException e)
            {
                exception = e;
                string message = string.Format(
                    "The {0} function '{1}' failed. See the function execution logs for details.",
                    functionType.ToString().ToLowerInvariant(),
                    functionName);
                throw new FunctionFailedException(message, e);
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
                        this.config.HubName,
                        functionName,
                        version,
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
                    this.config.HubName,
                    functionName,
                    version,
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
                object tcs;
                if (this.pendingExternalEvents.TryGetValue(name, out tcs))
                {
                    Type tcsType = tcs.GetType();
                    Type genericTypeArgument = tcsType.GetGenericArguments()[0];

                    object deserializedObject = MessagePayloadDataConverter.Default.Deserialize(input, genericTypeArgument);
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
