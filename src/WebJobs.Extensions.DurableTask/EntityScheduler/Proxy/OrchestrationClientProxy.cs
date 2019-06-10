// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        public Task InvokeAsync(EntityId entityId, string operationName, object operationInput)
        {
            return this.client.SignalEntityAsync(entityId, operationName, operationInput);
        }

        public async Task<TResult> InvokeAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            await this.client.SignalEntityAsync(entityId, operationName, operationInput);

            return default(TResult);
        }
    }
}