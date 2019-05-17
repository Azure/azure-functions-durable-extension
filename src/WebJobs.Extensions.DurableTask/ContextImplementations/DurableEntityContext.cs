// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using DurableTask.Core;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Context object passed to application code executing entity operations.
    /// </summary>
    internal class DurableEntityContext : DurableCommonContext, IDurableEntityContext
    {
        private readonly EntityId self;

        private List<OutgoingMessage> outbox = new List<OutgoingMessage>();

        public DurableEntityContext(DurableTaskExtension config, EntityId entity)
            : base(config, entity.EntityName)
        {
            this.self = entity;
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

        void IDurableEntityContext.DestructOnExit()
        {
            this.ThrowIfInvalidAccess();
            this.DestructOnExit = true;
        }

        T IDurableEntityContext.GetInput<T>()
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput<T>();
        }

        object IDurableEntityContext.GetInput(Type argumentType)
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetInput(argumentType);
        }

        T IDurableEntityContext.GetState<T>(Func<T> initializer)
        {
            this.ThrowIfInvalidAccess();

            if (!this.StateWasAccessed)
            {
                var result = (this.State.EntityState == null)
                    ? (initializer != null ? initializer() : default(T))
                    : MessagePayloadDataConverter.Default.Deserialize<T>(this.State.EntityState);
                this.CurrentState = result;
                this.StateWasAccessed = true;
                return result;
            }
            else
            {
                return (T)this.CurrentState;
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

        internal override void SendEntityMessage(OrchestrationInstance target, string eventName, object eventContent)
        {
            lock (this.outbox)
            {
                this.outbox.Add(new OutgoingMessage()
                {
                    Target = target,
                    EventName = eventName,
                    EventContent = eventContent,
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
