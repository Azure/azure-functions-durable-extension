// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableHttpMessageHandlerFactory : IDurableHttpMessageHandlerFactory
    {
        private HttpMessageHandler httpClientHandler;

        public DurableHttpMessageHandlerFactory()
        {
        }

        internal DurableHttpMessageHandlerFactory(HttpMessageHandler handler)
        {
            this.httpClientHandler = handler;
        }

        public HttpMessageHandler CreateHttpMessageHandler()
        {
            if (this.httpClientHandler == null)
            {
                this.httpClientHandler = new HttpClientHandler();
            }

            return this.httpClientHandler;
        }
    }
}
