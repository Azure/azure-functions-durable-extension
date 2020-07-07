// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Implementations of this interface can be used to provide authorization tokens for outbound HTTP requests.
    /// </summary>
    public interface ITokenSource
    {
        /// <summary>
        /// Gets a token for a resource.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<string> GetTokenAsync();
    }
}
