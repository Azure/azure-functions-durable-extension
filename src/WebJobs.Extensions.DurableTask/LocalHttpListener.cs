﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.WebApiCompatShim;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LocalHttpListener : IDisposable
    {
        private const int DefaultPort = 17071;

        private readonly DurableTaskExtension extension;
        private readonly IWebHost localWebHost;
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> handler;

        public LocalHttpListener(
            DurableTaskExtension extension,
            Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
#if !FUNCTIONS_V1
            this.InternalRpcUri = new Uri($"http://127.0.0.1:{this.GetAvailablePort()}/durabletask/");
            var listenUri = new Uri(this.InternalRpcUri.GetLeftPart(UriPartial.Authority));
            this.localWebHost = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(listenUri.OriginalString)
                .Configure(a => a.Run(this.HandleRequestAsync))
                .Build();
#else
            // Just use default port for internal Uri. No need to check for port availability since
            // we won't be listening to this endpoint.
            this.InternalRpcUri = new Uri($"http://127.0.0.1:{DefaultPort}/durabletask/");
            this.localWebHost = new NoOpWebHost();
#endif
        }

        public Uri InternalRpcUri { get; }

        public bool IsListening { get; private set; }

        public void Dispose() => this.localWebHost.Dispose();

        public async Task StartAsync()
        {
            if (this.IsListening == true)
            {
                throw new InvalidOperationException("The local HTTP listener has already started.");
            }

#if !FUNCTIONS_V1
            await this.localWebHost.StartAsync();
            this.IsListening = true;
#else
            // no-op: this is dummy code to make build warnings go away
            await Task.Yield();
#endif
        }

        public int GetAvailablePort()
        {
            // If we are able to successfully start a listener looking on the default port without
            // an exception, we can use the default port. Otherwise, let the TcpListener class decide for us.
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, DefaultPort);
                listener.Start();
                listener.Stop();
                return DefaultPort;
            }
            catch (SocketException)
            {
                // Following guidance of this stack overflow answer
                // to find available port: https://stackoverflow.com/a/150974/9035640
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int availablePort = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return availablePort;
            }
        }

        public async Task StopAsync()
        {
#if !FUNCTIONS_V1
            await this.localWebHost.StopAsync();
#else
            // no-op: this is dummy code to make build warnings go away
            await Task.Yield();
#endif
            this.IsListening = false;
        }

        private IWebHost CreateWebHost(Uri listenUri)
        {
            if (listenUri == null)
            {
                throw new ArgumentNullException(nameof(listenUri));
            }

            if (listenUri.AbsolutePath.Length > 1)
            {
                throw new ArgumentException($"The listen URL must not contain a path.", nameof(listenUri));
            }

#if !FUNCTIONS_V1
            return new WebHostBuilder()
                .UseKestrel()
                .UseUrls(listenUri.OriginalString)
                .Configure(a => a.Run(this.HandleRequestAsync))
                .Build();
#else
            return new NoOpWebHost();
#endif
        }

        private async Task HandleRequestAsync(HttpContext context)
        {
            try
            {
                HttpRequestMessage request = GetRequest(context);
                HttpResponseMessage response = await this.handler(request);
                await SetResponseAsync(context, response);
            }
            catch (Exception e)
            {
                this.extension.TraceHelper.ExtensionWarningEvent(
                    this.extension.Options.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: $"Unhandled exception in HTTP API handler: {e}");

                context.Response.StatusCode = 500;
            }
        }

        private static HttpRequestMessage GetRequest(HttpContext context)
        {
            return new HttpRequestMessageFeature(context).HttpRequestMessage;
        }

        // Copied from https://github.com/aspnet/Proxy/blob/148a5ea41393ef9e1ac319eef61dc3593a370c92/src/Microsoft.AspNetCore.Proxy/ProxyAdvancedExtensions.cs#L172-L196
        private static async Task SetResponseAsync(HttpContext context, HttpResponseMessage responseMessage)
        {
            HttpResponse response = context.Response;

            response.StatusCode = (int)responseMessage.StatusCode;
            foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            if (responseMessage.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Content.Headers)
                {
                    response.Headers[header.Key] = header.Value.ToArray();
                }

                using (Stream responseStream = await responseMessage.Content.ReadAsStreamAsync())
                {
                    await responseStream.CopyToAsync(response.Body, 81920, context.RequestAborted);
                }
            }
        }

#if FUNCTIONS_V1
        private class NoOpWebHost : IWebHost
        {
            public IFeatureCollection ServerFeatures => throw new NotImplementedException();

            public IServiceProvider Services => throw new NotImplementedException();

            public void Dispose() { }

            public void Start() { }

            public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;

            public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
        }
#endif
    }
}