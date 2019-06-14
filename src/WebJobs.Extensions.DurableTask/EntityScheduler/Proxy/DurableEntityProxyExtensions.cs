// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class DurableEntityProxyExtensions
    {
        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <param name="client">orchestration client.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        public static Task SignalEntityAsync<TEntityInterface>(this IDurableOrchestrationClient client, EntityId entityId, Action<TEntityInterface> operation)
        {
            var proxyContext = new OrchestrationClientProxy(client);
            var proxy = EntityProxyFactory.Create<TEntityInterface>(proxyContext, entityId);

            operation(proxy);

            if (proxyContext.SignalTask == null)
            {
                throw new InvalidOperationException("The operation action must perform an operation on the entity");
            }

            return proxyContext.SignalTask;
        }

        /// <summary>
        /// Create Entity Proxy.
        /// </summary>
        /// <param name="context">orchestration context.</param>
        /// <param name="entityId">Entity id.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <returns>Entity proxy.</returns>
        public static TEntityInterface CreateEntityProxy<TEntityInterface>(this IDurableOrchestrationContext context, EntityId entityId)
        {
            return EntityProxyFactory.Create<TEntityInterface>(new OrchestrationContextProxy(context), entityId);
        }

        /// <summary>
        /// Create Entity Proxy.
        /// </summary>
        /// <param name="context">entity context.</param>
        /// <param name="entityId">Entity id.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <returns>Entity proxy.</returns>
        public static TEntityInterface CreateEntityProxy<TEntityInterface>(this IDurableEntityContext context, EntityId entityId)
        {
            return EntityProxyFactory.Create<TEntityInterface>(new EntityContextProxy(context), entityId);
        }
    }
}