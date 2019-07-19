// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Defines convenient overloads for creating entity proxy, for all the contexts.
    /// </summary>
    public static class DurableEntityProxyExtensions
    {
        private static readonly ConcurrentDictionary<Type, Type> EntityNameMappings = new ConcurrentDictionary<Type, Type>();

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <param name="client">orchestration client.</param>
        /// <param name="entityKey">The target entity key.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        public static Task SignalEntityAsync<TEntityInterface>(this IDurableOrchestrationClient client, string entityKey, Action<TEntityInterface> operation)
        {
            return SignalEntityAsync<TEntityInterface>(client, new EntityId(ResolveEntityName<TEntityInterface>(), entityKey), operation);
        }

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
        /// Create an entity proxy.
        /// </summary>
        /// <param name="context">orchestration context.</param>
        /// <param name="entityKey">The target entity key.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <returns>Entity proxy.</returns>
        public static TEntityInterface CreateEntityProxy<TEntityInterface>(this IDurableOrchestrationContext context, string entityKey)
        {
            return CreateEntityProxy<TEntityInterface>(context, new EntityId(ResolveEntityName<TEntityInterface>(), entityKey));
        }

        /// <summary>
        /// Create an entity proxy.
        /// </summary>
        /// <param name="context">orchestration context.</param>
        /// <param name="entityId">The target entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        /// <returns>Entity proxy.</returns>
        public static TEntityInterface CreateEntityProxy<TEntityInterface>(this IDurableOrchestrationContext context, EntityId entityId)
        {
            return EntityProxyFactory.Create<TEntityInterface>(new OrchestrationContextProxy(context), entityId);
        }

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <param name="context">entity context.</param>
        /// <param name="entityKey">The target entity key.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        public static void SignalEntity<TEntityInterface>(this IDurableEntityContext context, string entityKey, Action<TEntityInterface> operation)
        {
            SignalEntity<TEntityInterface>(context, new EntityId(ResolveEntityName<TEntityInterface>(), entityKey), operation);
        }

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <param name="context">entity context.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="operation">A delegate that performs the desired operation on the entity.</param>
        /// <typeparam name="TEntityInterface">Entity interface.</typeparam>
        public static void SignalEntity<TEntityInterface>(this IDurableEntityContext context, EntityId entityId, Action<TEntityInterface> operation)
        {
            operation(EntityProxyFactory.Create<TEntityInterface>(new EntityContextProxy(context), entityId));
        }

        private static string ResolveEntityName<TEntityInterface>()
        {
            var type = EntityNameMappings.GetOrAdd(typeof(TEntityInterface), CreateTypeMapping);

            return type.Name;
        }

        private static Type CreateTypeMapping(Type interfaceType)
        {
            var implementedTypes = interfaceType.Assembly
                                                .GetTypes()
                                                .Where(x => x.IsClass && !x.IsAbstract && interfaceType.IsAssignableFrom(x))
                                                .ToArray();

            if (!implementedTypes.Any())
            {
                throw new InvalidOperationException($"Cannot find class that implements {interfaceType.FullName}.");
            }

            if (implementedTypes.Length > 1)
            {
                throw new InvalidOperationException($"Ambiguous derived class with implemented {interfaceType.FullName}.");
            }

            return implementedTypes[0];
        }
    }
}