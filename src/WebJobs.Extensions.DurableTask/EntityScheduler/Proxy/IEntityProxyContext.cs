// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Abstract entity proxy context.
    /// </summary>
    public interface IEntityProxyContext
    {
        /// <summary>
        /// Invoke entity function.
        /// </summary>
        /// <param name="entityId">Entity id.</param>
        /// <param name="operationName">Entity operation name.</param>
        /// <param name="operationInput">Entity input value.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task InvokeAsync(EntityId entityId, string operationName, object operationInput);

        /// <summary>
        /// Invoke entity function.
        /// </summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="entityId">Entity id.</param>
        /// <param name="operationName">Entity operation name.</param>
        /// <param name="operationInput">Entity input value.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<TResult> InvokeAsync<TResult>(EntityId entityId, string operationName, object operationInput);
    }
}
