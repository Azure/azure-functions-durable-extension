// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class DurableHttpMessageHandlerFactory : IDurableHttpMessageHandlerFactory
    {
        private readonly bool hasCustomHandler;
        private HttpMessageHandler httpClientHandler;

        public DurableHttpMessageHandlerFactory()
        {
        }

        internal DurableHttpMessageHandlerFactory(HttpMessageHandler handler)
        {
            this.httpClientHandler = handler;
            this.hasCustomHandler = true;
        }

        internal HttpMessageHandler CreatePollingMessageHandler()
        {
            if (this.hasCustomHandler)
            {
                throw new NotSupportedException("Configured polling is not supported with a custom HTTP message handler.");
            }

            return new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
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
