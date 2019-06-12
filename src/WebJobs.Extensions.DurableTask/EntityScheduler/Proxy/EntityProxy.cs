// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Entity Proxy class.
    /// </summary>
    public abstract class EntityProxy
    {
        private readonly IEntityProxyContext context;
        private readonly EntityId entityId;

        /// <summary>
        /// Create entity proxy.
        /// </summary>
        /// <param name="context">context.</param>
        /// <param name="entityId">Entity id.</param>
        protected EntityProxy(IEntityProxyContext context, EntityId entityId)
        {
            this.context = context;
            this.entityId = entityId;
        }

        /// <summary>
        /// Call entity function.
        /// </summary>
        /// <param name="operationName">Entity operation name.</param>
        /// <param name="operationInput">Entity input value.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        protected internal Task CallAsync(string operationName, object operationInput)
        {
            return this.context.CallAsync(this.entityId, operationName, operationInput);
        }

        /// <summary>
        /// Call entity function.
        /// </summary>
        /// <typeparam name="TResult">return type.</typeparam>
        /// <param name="operationName">Entity operation name.</param>
        /// <param name="operationInput">Entity input value.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        protected internal Task<TResult> CallAsync<TResult>(string operationName, object operationInput)
        {
            return this.context.CallAsync<TResult>(this.entityId, operationName, operationInput);
        }

        /// <summary>
        /// Signal entity function.
        /// </summary>
        /// <param name="operationName">Entity operation name.</param>
        /// <param name="operationInput">Entity input value.</param>
        protected internal void Signal(string operationName, object operationInput)
        {
            this.context.Signal(this.entityId, operationName, operationInput);
        }
    }
}
