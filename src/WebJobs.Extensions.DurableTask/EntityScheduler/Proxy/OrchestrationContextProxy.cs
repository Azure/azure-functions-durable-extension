// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    internal class OrchestrationContextProxy : IEntityProxyContext
    {
        private readonly IDurableOrchestrationContext context;

        internal OrchestrationContextProxy(IDurableOrchestrationContext context)
        {
            this.context = context;
        }

        public Task InvokeAsync(EntityId entityId, string operationName, object operationInput)
        {
            return this.context.CallEntityAsync(entityId, operationName, operationInput);
        }

        public Task<TResult> InvokeAsync<TResult>(EntityId entityId, string operationName, object operationInput)
        {
            return this.context.CallEntityAsync<TResult>(entityId, operationName, operationInput);
        }
    }
}