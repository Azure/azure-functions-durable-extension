// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface used for testing Durable HTTP.
    /// </summary>
    public interface IDurableHttpMessageHandlerFactory
    {
        /// <summary>
        /// Creates an HttpClientHandler and returns it.
        /// </summary>
        /// <returns>Returns an HttpClientHandler.</returns>
        HttpMessageHandler CreateHttpMessageHandler();
    }
}
