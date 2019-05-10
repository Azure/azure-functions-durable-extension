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
        private const int MaxTimerDurationInDays = 6;

        private string serializedOutput;
        private string serializedCustomStatus;

        private int newGuidCounter = 0;

        private LockReleaser lockReleaser = null;

        internal DurableOrchestrationContext(DurableTaskExtension config, string functionName)
            : base(config, functionName)
        {
        }

        internal bool ContinuedAsNew { get; private set; }

        internal bool IsOutputSet => this.serializedOutput != null;

        private string OrchestrationName => this.FunctionName;

        internal override FunctionType FunctionType => FunctionType.Orchestrator;

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
            return this.WaitForExternalEvent<T>(name, "ExternalEvent");
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
        Task<TResult> IInterleavingContext.CallEntityAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            return this.CallDurableTaskFunctionAsync<TResult>(entityId.EntityName, FunctionType.Entity, false, EntityId.GetSchedulerIdFromEntityId(entityId), operationName, null, operationInput);
        }

        /// <inheritdoc/>
        Task IInterleavingContext.CallEntityAsync(EntityId entityId, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            return this.CallDurableTaskFunctionAsync<object>(entityId.EntityName, FunctionType.Entity, false, EntityId.GetSchedulerIdFromEntityId(entityId), operationName, null, operationInput);
        }

        /// <inheritdoc/>
        async Task<IDisposable> IInterleavingContext.LockAsync(params EntityId[] entities)
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

        internal override void SendEntityMessage(OrchestrationInstance target, string eventName, object eventContent)
        {
            if (!this.IsReplaying)
            {
                this.Config.TraceHelper.SendingEntityMessage(
                    this.InstanceId,
                    this.ExecutionId,
                    target.InstanceId,
                    eventName,
                    eventContent);
            }

            this.InnerContext.SendEvent(target, eventName, eventContent);
        }

        internal override Guid NewGuid()
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
