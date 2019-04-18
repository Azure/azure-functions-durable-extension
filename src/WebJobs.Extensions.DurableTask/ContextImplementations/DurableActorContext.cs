// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Context object passed to application code executing actor operations.
    /// </summary>
    internal class DurableActorContext : DurableCommonContext, IDurableActorContext
    {
        private readonly ActorId self;

        public DurableActorContext(DurableTaskExtension config, ActorId actor)
         : base(config, actor.ActorClass)
        {
            this.self = actor;
        }

        internal bool StateWasAccessed { get; set; }

        internal object CurrentState { get; set; }

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

        T IDurableActorContext.GetState<T>()
        {
            this.ThrowIfInvalidAccess();

            if (!this.StateWasAccessed)
            {
                var result = (this.State.ActorState == null)
                    ? default(T)
                    : JsonConvert.DeserializeObject<T>(this.State.ActorState);
                this.CurrentState = result;
                this.StateWasAccessed = true;
                return result;
            }
            else
            {
                return (T)this.CurrentState;
            }
        }

        void IDurableActorContext.SetState(object o)
        {
            this.ThrowIfInvalidAccess();

            this.CurrentState = o;
            this.StateWasAccessed = true;
        }

        internal void Writeback()
        {
            if (this.StateWasAccessed)
            {
                this.State.ActorState = JsonConvert.SerializeObject(this.CurrentState);

                this.CurrentState = null;
                this.StateWasAccessed = false;
            }
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

            base.ThrowIfInvalidAccess();
        }
    }
}
