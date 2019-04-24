// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using DurableTask.Core;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Context object passed to application code executing actor operations.
    /// </summary>
    internal class DurableActorContext : DurableCommonContext, IDurableActorContext
    {
        private readonly ActorId self;

        private List<OutgoingMessage> outbox;

        public DurableActorContext(DurableTaskExtension config, ActorId actor)
            : base(config, actor.ActorClass)
        {
            this.self = actor;
        }

        internal IStateView CurrentStateView { get; set; }

        internal SchedulerState State { get; set; }

        internal RequestMessage CurrentOperation { get; set; }

        internal DateTime CurrentOperationStartTime { get; set; }

        internal ResponseMessage CurrentOperationResponse { get; set; }

        internal bool IsNewlyConstructed { get; set; }

        internal bool DestructOnExit { get; set; }

        string IDurableActorContext.ActorClass => this.self.ActorClass;

        string IDurableActorContext.Key => this.self.ActorKey;

        ActorId IDurableActorContext.Self => this.self;

        internal override FunctionType FunctionType => FunctionType.Actor;

        string IDurableActorContext.OperationName
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.CurrentOperation.Operation;
            }
        }

        bool IDurableActorContext.IsNewlyConstructed
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.IsNewlyConstructed;
            }
        }

        void IDurableActorContext.DestructOnExit()
        {
            this.ThrowIfInvalidAccess();
            this.DestructOnExit = true;
        }

        T IDurableActorContext.GetOperationContent<T>()
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetContent<T>();
        }

        object IDurableActorContext.GetOperationContent(Type argumentType)
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetContent(argumentType);
        }

        IStateView<TState> IDurableActorContext.GetState<TState>(Formatting formatting, JsonSerializerSettings settings)
        {
            this.ThrowIfInvalidAccess();
            if (this.CurrentStateView != null)
            {
                // if the requested type is the same, we can use the already existing view
                // otherwise we have to serialize the current view to JSON, and then
                // deserialize it to the requested type
                if (this.CurrentStateView is IStateView<TState> view)
                {
                    return view;
                }

                this.CurrentStateView.Dispose();
            }

            var newView = new TypedStateView<TState>(this, formatting, settings);
            this.CurrentStateView = newView;
            return newView;
        }

        void IDurableActorContext.SignalActor(ActorId actor, string operationName, object operationContent)
        {
            this.ThrowIfInvalidAccess();
            var alreadyCompletedTask = this.CallDurableTaskFunctionAsync<object>(actor.ActorClass, FunctionType.Actor, true, ActorId.GetSchedulerIdFromActorId(actor), operationName, null, operationContent);
            System.Diagnostics.Debug.Assert(alreadyCompletedTask.IsCompleted, "signalling actors is synchronous");
            alreadyCompletedTask.Wait(); // just so we see exceptions during testing
        }

        void IDurableActorContext.Return(object result)
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

        internal override void SendActorMessage(OrchestrationInstance target, string eventName, object eventContent)
        {
            lock (this)
            {
                if (this.outbox == null)
                {
                    this.outbox = new List<OutgoingMessage>();
                }

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
            if (this.outbox != null)
            {
                foreach (var message in this.outbox)
                {
                    this.Config.TraceHelper.SendingActorMessage(
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
