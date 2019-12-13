// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class EntityClientProxy : IEntityProxyContext
    {
        private readonly IDurableEntityClient client;
        private readonly DateTime? scheduledTimeForSignal;

        internal EntityClientProxy(IDurableEntityClient client)
        {
            this.client = client;
        }

        internal EntityClientProxy(IDurableEntityClient client, DateTime scheduledTimeForSignal)
        {
            this.client = client;
            this.scheduledTimeForSignal = scheduledTimeForSignal;
        }

        internal Task SignalTask { get; private set; }

        public Task CallAsync(EntityId entityId, string operationName, object operationInput)
        {
            this.SignalAndStoreTask(entityId, operationName, operationInput);
            return Task.CompletedTask;
        }

        public Task<TResult> CallAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            this.SignalAndStoreTask(entityId, operationName, operationInput);
            return Task.FromResult<TResult>(default(TResult));
        }

        public void Signal(EntityId entityId, string operationName, object operationInput)
        {
            this.SignalAndStoreTask(entityId, operationName, operationInput);
        }

        private void SignalAndStoreTask(EntityId entityId, string operationName, object operationInput)
        {
            if (this.SignalTask != null)
            {
                throw new InvalidOperationException("The operation action must not perform more than one operation");
            }

            if (this.scheduledTimeForSignal.HasValue)
            {
                this.SignalTask = this.client.SignalEntityAsync(entityId, this.scheduledTimeForSignal.Value, operationName, operationInput);
            }
            else
            {
                this.SignalTask = this.client.SignalEntityAsync(entityId, operationName, operationInput);
            }
        }
    }
}