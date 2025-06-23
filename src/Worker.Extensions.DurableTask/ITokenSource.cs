// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask
{
    /// <summary>
    /// Implementations of this interface can be used to provide authorization tokens for outbound HTTP requests.
    /// This calss is the same as the one in WebJobs.Extensions.DurableTask/ITokenSource.cs
    /// The implementation is kept in sync to ensure compatibility.
    /// </summary>
    public interface ITokenSource
    {
        /// <summary>
        /// Gets a token for a resource.
        /// </summary>
        Task<string> GetTokenAsync();
    }
}
