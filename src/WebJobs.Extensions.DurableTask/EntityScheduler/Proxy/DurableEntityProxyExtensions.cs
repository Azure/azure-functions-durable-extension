// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods.
    /// </summary>
    public static class DurableEntityProxyExtensions
    {
        /// <summary>
        /// Create Entity Proxy.
        /// </summary>
        /// <param name="client">orchestration client.</param>
        /// <param name="entityId">Entity id.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <returns>Entity proxy.</returns>
        public static TEntityInterface CreateEntityProxy<TEntityInterface>(this IDurableOrchestrationClient client, EntityId entityId)
        {
            return EntityProxyFactory.Create<TEntityInterface>(new OrchestrationClientProxy(client), entityId);
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