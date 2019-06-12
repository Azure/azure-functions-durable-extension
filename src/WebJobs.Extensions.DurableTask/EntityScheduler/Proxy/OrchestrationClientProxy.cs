// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    internal class OrchestrationClientProxy : IEntityProxyContext
    {
        private readonly IDurableOrchestrationClient client;

        internal OrchestrationClientProxy(IDurableOrchestrationClient client)
        {
            this.client = client;
        }

        public Task CallAsync(EntityId entityId, string operationName, object operationInput)
        {
            throw new NotSupportedException();
        }

        public Task<TResult> CallAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            throw new NotSupportedException();
        }

        public void Signal(EntityId entityId, string operationName, object operationInput)
        {
            this.client.SignalEntityAsync(entityId, operationName, operationInput).GetAwaiter().GetResult();
        }
    }
}