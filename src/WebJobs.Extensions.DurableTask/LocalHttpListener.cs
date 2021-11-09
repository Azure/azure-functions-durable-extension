// Copyright (c) .NET Foundation. All rights reserved.
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

        // Pick a large, fixed range of ports that are going to be valid in all environment.
        // Avoiding ports below 1024 as those are blocked by app service sandbox.
        // Ephemeral ports for most OS start well above 32768. See https://www.ncftp.com/ncftpd/doc/misc/ephemeral_ports.html
        private const int MinPort = 30000;
        private const int MaxPort = 31000;

        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> handler;
        private readonly EndToEndTraceHelper traceHelper;
        private readonly DurableTaskOptions durableTaskOptions;
        private readonly Random portGenerator;
        private readonly HashSet<int> attemptedPorts;

        private IWebHost localWebHost;

        public LocalHttpListener(
            EndToEndTraceHelper traceHelper,
            DurableTaskOptions durableTaskOptions,
            Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            this.traceHelper = traceHelper ?? throw new ArgumentNullException(nameof(traceHelper));
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            this.durableTaskOptions = durableTaskOptions ?? throw new ArgumentNullException(nameof(durableTaskOptions));

            // Set to a non null value
            this.InternalRpcUri = new Uri($"http://uninitialized");
            this.localWebHost = new NoOpWebHost();
            this.portGenerator = new Random();
            this.attemptedPorts = new HashSet<int>();
        }

        public Uri InternalRpcUri { get; private set; }

        public bool IsListening { get; private set; }

        public void Dispose() => this.localWebHost.Dispose();

        public async Task StartAsync()
        {
            if (this.IsListening == true)
            {
                throw new InvalidOperationException("The local HTTP listener has already started.");
            }

#if !FUNCTIONS_V1
            const int maxAttempts = 10;
            int numAttempts = 1;
            do
            {
                int listeningPort = numAttempts == 1
                    ? DefaultPort
                    : this.GetRandomPort();
                try
                {
                    this.InternalRpcUri = new Uri($"http://127.0.0.1:{listeningPort}/durabletask/");
                    var listenUri = new Uri(this.InternalRpcUri.GetLeftPart(UriPartial.Authority));
                    this.localWebHost = new WebHostBuilder()
                        .UseKestrel()
                        .UseUrls(listenUri.OriginalString)
                        .Configure(a => a.Run(this.HandleRequestAsync))
                        .Build();

                    await this.localWebHost.StartAsync();
                    this.IsListening = true;
                    break;
                }
                catch (IOException)
                {
                    this.traceHelper.ExtensionWarningEvent(
                        this.durableTaskOptions.HubName,
                        functionName: string.Empty,
                        instanceId: string.Empty,
                        message: $"Failed to open local port {listeningPort}. This was attempt #{numAttempts} to open a local port.");
                    this.attemptedPorts.Add(listeningPort);
                    numAttempts++;
                }
            }
            while (numAttempts <= maxAttempts);

            if (!this.IsListening)
            {
                throw new IOException($"Unable to find a port to open an RPC endpoint on after {maxAttempts} attempts");
            }
#else
            // no-op: this is dummy code to make build warnings go away
            await Task.Yield();
#endif
        }

        private int GetRandomPort()
        {
            // Get a random port that has not already been attempted so we don't waste time trying
            // to listen to a port we know is not free.
            int randomPort;
            do
            {
                randomPort = this.portGenerator.Next(MinPort, MaxPort);
            }
            while (this.attemptedPorts.Contains(randomPort));

            return randomPort;
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
                this.traceHelper.ExtensionWarningEvent(
                    this.durableTaskOptions.HubName,
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

        private class NoOpWebHost : IWebHost
        {
            public IFeatureCollection ServerFeatures => throw new NotImplementedException();

            public IServiceProvider Services => throw new NotImplementedException();

            public void Dispose() { }

            public void Start() { }

            public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;

            public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
        }
    }
}