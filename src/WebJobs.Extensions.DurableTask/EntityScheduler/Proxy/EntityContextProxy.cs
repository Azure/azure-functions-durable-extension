// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EntityContextProxy : IEntityProxyContext
    {
        private readonly IDurableEntityContext context;
        private readonly DateTime? scheduledTimeForSignal;

        internal EntityContextProxy(IDurableEntityContext context)
        {
            this.context = context;
        }

        internal EntityContextProxy(IDurableEntityContext context, DateTime scheduledTimeForSignal)
        {
            this.context = context;
            this.scheduledTimeForSignal = scheduledTimeForSignal;
        }

        public Task CallAsync(EntityId entityId, string operationName, object operationInput)
        {
            if (this.scheduledTimeForSignal.HasValue)
            {
                this.context.SignalEntity(entityId, this.scheduledTimeForSignal.Value, operationName, operationInput);
            }
            else
            {
                this.context.SignalEntity(entityId, operationName, operationInput);
            }

            return Task.CompletedTask;
        }

        public Task<TResult> CallAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            if (this.scheduledTimeForSignal.HasValue)
            {
                this.context.SignalEntity(entityId, this.scheduledTimeForSignal.Value, operationName, operationInput);
            }
            else
            {
                this.context.SignalEntity(entityId, operationName, operationInput);
            }

            return Task.FromResult(default(TResult));
        }

        public void Signal(EntityId entityId, string operationName, object operationInput)
        {
            if (this.scheduledTimeForSignal.HasValue)
            {
                this.context.SignalEntity(entityId, this.scheduledTimeForSignal.Value, operationName, operationInput);
            }
            else
            {
                this.context.SignalEntity(entityId, operationName, operationInput);
            }
        }
    }
}