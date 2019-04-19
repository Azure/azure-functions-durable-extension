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
    /// Context object passed to application code executing entity operations.
    /// </summary>
    internal class DurableEntityContext : DurableCommonContext, IDurableEntityContext
    {
        private readonly EntityId self;

        public DurableEntityContext(DurableTaskExtension config, EntityId entity)
         : base(config, entity.EntityName)
        {
            this.self = entity;
        }

        internal IStateView CurrentStateView { get; set; }

        internal SchedulerState State { get; set; }

        internal RequestMessage CurrentOperation { get; set; }

        internal DateTime CurrentOperationStartTime { get; set; }

        internal ResponseMessage CurrentOperationResponse { get; set; }

        internal bool IsNewlyConstructed { get; set; }

        internal bool DestructOnExit { get; set; }

        string IDurableEntityContext.EntityName => this.self.EntityName;

        string IDurableEntityContext.Key => this.self.EntityKey;

        EntityId IDurableEntityContext.Self => this.self;

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

        T IDurableEntityContext.GetOperationContent<T>()
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetContent<T>();
        }

        object IDurableEntityContext.GetOperationContent(Type argumentType)
        {
            this.ThrowIfInvalidAccess();
            return this.CurrentOperation.GetContent(argumentType);
        }

        IStateView<TState> IDurableEntityContext.GetState<TState>(Formatting formatting, JsonSerializerSettings settings)
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

            base.ThrowIfInvalidAccess();
        }
    }
}
