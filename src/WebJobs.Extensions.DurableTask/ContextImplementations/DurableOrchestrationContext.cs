// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
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
    internal class DurableOrchestrationContext : DurableCommonContext, IDurableOrchestrationContext
    {
        private const int MaxTimerDurationInDays = 6;

        private string serializedOutput;
        private string serializedCustomStatus;

        internal DurableOrchestrationContext(DurableTaskExtension config, string functionName)
            : base(config, functionName)
        {
        }

        internal bool ContinuedAsNew { get; private set; }

        internal bool IsOutputSet => this.serializedOutput != null;

        private string OrchestrationName => this.FunctionName;

        /// <inheritdoc />
        string IDurableOrchestrationContext.InstanceId => this.InstanceId;

        /// <inheritdoc />
        string IDurableOrchestrationContext.ParentInstanceId => this.ParentInstanceId;

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
        Task<TResult> IInterleavingContext.CallSubOrchestratorAsync<TResult>(string functionName, string instanceId, object input)
        {
            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, false, instanceId, null, null, input);
        }

        /// <inheritdoc />
        Task<TResult> IInterleavingContext.CallSubOrchestratorWithRetryAsync<TResult>(string functionName, RetryOptions retryOptions, string instanceId, object input)
        {
            if (retryOptions == null)
            {
                throw new ArgumentNullException(nameof(retryOptions));
            }

            return this.CallDurableTaskFunctionAsync<TResult>(functionName, FunctionType.Orchestrator, false, instanceId, null, retryOptions, input);
        }

        /// <inheritdoc />
        async Task<T> IInterleavingContext.CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken)
        {
            this.ThrowIfInvalidAccess();

            // This check can be removed once the storage provider supports extended timers.
            // https://github.com/Azure/azure-functions-durable-extension/issues/14
            if (fireAt.Subtract(this.InnerContext.CurrentUtcDateTime) > TimeSpan.FromDays(MaxTimerDurationInDays))
            {
                throw new ArgumentException($"Timer durations must not exceed {MaxTimerDurationInDays} days.", nameof(fireAt));
            }

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
        Task<T> IInterleavingContext.WaitForExternalEvent<T>(string name)
        {
            this.ThrowIfInvalidAccess();
            return this.WaitForExternalEvent<T>(name);
        }

        /// <inheritdoc/>
        Task<T> IInterleavingContext.WaitForExternalEvent<T>(string name, TimeSpan timeout)
        {
            this.ThrowIfInvalidAccess();
            Action<TaskCompletionSource<T>> timedOutAction = cts =>
                cts.TrySetException(new TimeoutException($"Event {name} not received in {timeout}"));
            return this.WaitForExternalEvent(name, timeout, timedOutAction);
        }

        /// <inheritdoc/>
        Task<T> IInterleavingContext.WaitForExternalEvent<T>(string name, TimeSpan timeout, T defaultValue)
        {
            this.ThrowIfInvalidAccess();
            Action<TaskCompletionSource<T>> timedOutAction = cts => cts.TrySetResult(defaultValue);
            return this.WaitForExternalEvent(name, timeout, timedOutAction);
        }

        private Task<T> WaitForExternalEvent<T>(string name, TimeSpan timeout, Action<TaskCompletionSource<T>> timeoutAction)
        {
            var tcs = new TaskCompletionSource<T>();
            var cts = new CancellationTokenSource();

            var timeoutAt = this.InnerContext.CurrentUtcDateTime + timeout;
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
        void IDurableOrchestrationContext.ContinueAsNew(object input)
        {
            this.ThrowIfInvalidAccess();
            this.InnerContext.ContinueAsNew(input);
            this.ContinuedAsNew = true;
        }

        /// <inheritdoc/>
        Task<TResult> IInterleavingContext.CallActorAsync<TResult>(ActorId actorId, string operationName, object operationContent)
        {
            this.ThrowIfInvalidAccess();
            return this.CallDurableTaskFunctionAsync<TResult>(actorId.ActorClass, FunctionType.Actor, false, TaskActorShim.GetSchedulerIdFromActorId(actorId), operationName, null, operationContent);
        }

        /// <inheritdoc/>
        Task IInterleavingContext.CallActorAsync(ActorId actorId, string operationName, object operationContent)
        {
            this.ThrowIfInvalidAccess();
            return this.CallDurableTaskFunctionAsync<object>(actorId.ActorClass, FunctionType.Actor, false, TaskActorShim.GetSchedulerIdFromActorId(actorId), operationName, null, operationContent);
        }

        ///<inheritdoc/>
        Task<IDisposable> IInterleavingContext.LockAsync(params ActorId[] actors)
        {
            this.ThrowIfInvalidAccess();
            throw new NotImplementedException(); // TODO
        }
    }
}
