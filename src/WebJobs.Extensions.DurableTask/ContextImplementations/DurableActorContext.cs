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
        private readonly ActorRef self;

        public DurableActorContext(DurableTaskExtension config, ActorRef actor)
         : base(config, actor.ActorClass)
        {
            this.self = actor;
        }

        internal SchedulerState State { get; set; }

        internal OperationMessage CurrentOperation { get; set; }

        internal ResponseMessage CurrentOperationResponse { get; set; }

        internal bool Fresh { get; set; }

        internal bool DestructOnExit { get; set; }

        string IDurableActorContext.ActorClass => this.self.ActorClass;

        string IDurableActorContext.Key => this.self.ActorKey;

        ActorRef IDurableActorContext.Self => this.self;

        string IDurableActorContext.OperationName
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.CurrentOperation.Operation;
            }
        }

        ILogger IDurableActorContext.Logger => throw new NotImplementedException();

        bool IDurableActorContext.NewlyConstructed
        {
            get
            {
                this.ThrowIfInvalidAccess();
                return this.Fresh;
            }
        }

        void IDurableActorContext.DestructOnExit()
        {
            this.ThrowIfInvalidAccess();
            this.DestructOnExit = true;
        }

        TArgument IDurableActorContext.GetArgument<TArgument>()
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetArgument<TArgument>();
        }

        object IDurableActorContext.GetArgument(Type argumentType)
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetArgument(argumentType);
        }

        IStateView<TState> IDurableActorContext.GetStateAs<TState>(Formatting formatting, JsonSerializerSettings settings)
        {
            this.ThrowIfInvalidAccess();
            if (this.State.CurrentStateView != null)
            {
                if (this.State.CurrentStateView is IStateView<TState> view)
                {
                    return view;
                }

                this.State.CurrentStateView.Dispose();
            }

            var newView = new TypedStateView<TState>(this, formatting, settings);
            this.State.CurrentStateView = newView;
            return newView;
        }

        void IDurableActorContext.Return<TResult>(TResult result)
        {
            this.ThrowIfInvalidAccess();
            this.CurrentOperationResponse.SetResult(result);
        }

        void IDurableActorContext.Return(object result, Type resultType)
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
