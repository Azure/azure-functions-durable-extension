// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class OrchestrationContextProxy : IEntityProxyContext
    {
        private readonly IDurableOrchestrationContext context;
        private readonly DateTime? scheduledTimeForSignal;

        internal OrchestrationContextProxy(IDurableOrchestrationContext context)
        {
            this.context = context;
        }

        internal OrchestrationContextProxy(IDurableOrchestrationContext context, DateTime scheduledTimeForSignal)
        {
            this.scheduledTimeForSignal = scheduledTimeForSignal;
        }

        public Task CallAsync(EntityId entityId, string operationName, object operationInput)
        {
            return this.context.CallEntityAsync(entityId, operationName, operationInput);
        }

        public Task<TResult> CallAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            return this.context.CallEntityAsync<TResult>(entityId, operationName, operationInput);
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