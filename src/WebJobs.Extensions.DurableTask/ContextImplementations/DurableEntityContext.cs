// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Context object passed to application code executing entity operations.
    /// </summary>
    internal class DurableEntityContext : DurableCommonContext, IDurableEntityContext
    {
        private readonly EntityId self;

        private readonly TaskEntityShim shim;

        private List<OutgoingMessage> outbox = new List<OutgoingMessage>();

        public DurableEntityContext(DurableTaskExtension config, EntityId entity, TaskEntityShim shim)
            : base(config, entity.EntityName)
        {
            this.self = entity;
            this.shim = shim;
        }

        internal bool StateWasAccessed { get; set; }

        internal object CurrentState { get; set; }

        internal SchedulerState State { get; set; }

        internal RequestMessage CurrentOperation { get; set; }

        internal DateTime CurrentOperationStartTime { get; set; }

        internal ResponseMessage CurrentOperationResponse { get; set; }

        internal bool IsNewlyConstructed { get; set; }

        internal bool DestructOnExit { get; set; }

        string IDurableEntityContext.EntityName => this.self.EntityName;

        string IDurableEntityContext.EntityKey => this.self.EntityKey;

        EntityId IDurableEntityContext.EntityId => this.self;

        internal override FunctionType FunctionType => FunctionType.Entity;

        internal List<RequestMessage> OperationBatch => this.shim.OperationBatch;

        internal EntityId Self => this.self;

        string IDurableEntityContext.OperationName
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.CurrentOperation.Operation;
            }
        }

        bool IDurableEntityContext.IsNewlyConstructed
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.IsNewlyConstructed;
            }
        }

#if NETSTANDARD2_0
        public FunctionBindingContext FunctionBindingContext { get; set; }
#endif

        void IDurableEntityContext.DestructOnExit()
        {
            this.ThrowIfInvalidAccess();
            this.DestructOnExit = true;
        }

        TInput IDurableEntityContext.GetInput<TInput>()
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput<TInput>();
        }

        object IDurableEntityContext.GetInput(Type argumentType)
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput(argumentType);
        }

        TState IDurableEntityContext.GetState<TState>(Func<TState> initializer)
        {
            this.ThrowIfInvalidAccess();

            if (!this.StateWasAccessed)
            {
                TState defaultValue = initializer != null ? initializer() : default(TState);

                if (this.State.EntityState != null)
                {
                    this.CurrentState = this.GetPopulatedState<TState>(defaultValue, initializer != null);
                }
                else
                {
                    this.CurrentState = defaultValue;
                }

                this.StateWasAccessed = true;
            }

            return (TState)this.CurrentState;
        }

        private TState GetPopulatedState<TState>(TState initialValue, bool usedTypeInitializer)
        {
            if (usedTypeInitializer)
            {
                // Only populate serialized state, as some fields may be populated by the initializer
                // using dependency injection.
                JsonConvert.PopulateObject(this.State.EntityState, initialValue);
                return initialValue;
            }
            else
            {
                return JsonConvert.DeserializeObject<TState>(this.State.EntityState);
            }
        }

        void IDurableEntityContext.SetState(object o)
        {
            this.ThrowIfInvalidAccess();

            this.CurrentState = o;
            this.StateWasAccessed = true;
        }

        internal void Writeback()
        {
            if (this.StateWasAccessed)
            {
                this.State.EntityState = MessagePayloadDataConverter.Default.Serialize(this.CurrentState);

                this.CurrentState = null;
                this.StateWasAccessed = false;
            }
        }

        void IDurableEntityContext.SignalEntity(EntityId entity, string operationName, object operationInput)
        {
            this.ThrowIfInvalidAccess();
            if (operationName == null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<object>(entity.EntityName, FunctionType.Entity, true, EntityId.GetSchedulerIdFromEntityId(entity), operationName, null, operationInput);
            System.Diagnostics.Debug.Assert(alreadyCompletedTask.IsCompleted, "signalling entities is synchronous");
            alreadyCompletedTask.Wait(); // just so we see exceptions during testing
        }

        void IDurableEntityContext.Return(object result)
        {
            this.ThrowIfInvalidAccess();
            this.CurrentOperationResponse.SetResult(result);
        }

        internal override void ThrowIfInvalidAccess()
        {
            if (this.CurrentOperation == null)
            {
                throw new InvalidOperationException("No operation is being processed.");
            }
        }

        internal override void SendEntityMessage(OrchestrationInstance target, string eventName, object message)
        {
            lock (this.outbox)
            {
                if (message is RequestMessage requestMessage)
                {
                    this.State.MessageSorter.LabelOutgoingMessage(requestMessage, target.InstanceId, DateTime.UtcNow, this.EntityMessageReorderWindow);
                }

                this.outbox.Add(new OutgoingMessage()
                {
                    Target = target,
                    EventName = eventName,
                    EventContent = message,
                });
            }
        }

        internal void SendOutbox(OrchestrationContext innerContext)
        {
            lock (this.outbox)
            {
                foreach (var message in this.outbox)
                {
                    this.Config.TraceHelper.SendingEntityMessage(
                        this.InstanceId,
                        this.ExecutionId,
                        message.Target.InstanceId,
                        message.EventName,
                        message.EventContent);

                    innerContext.SendEvent(message.Target, message.EventName, message.EventContent);
                }

                this.outbox.Clear();
            }
        }

        internal override Guid NewGuid()
        {
            return Guid.NewGuid();
        }

        private struct OutgoingMessage
        {
            public OrchestrationInstance Target;
            public string EventName;
            public object EventContent;
        }
    }
}
