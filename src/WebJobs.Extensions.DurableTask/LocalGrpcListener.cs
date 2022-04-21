// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#if NET6_0_OR_GREATER
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.DurableTask.Sidecar.Grpc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal class LocalGrpcListener : IHostedService, IDisposable
    {
        private const int DefaultPort = 4001;

        // Pick a large, fixed range of ports that are going to be valid in all environment.
        // Avoiding ports below 1024 as those are blocked by app service sandbox.
        // Ephemeral ports for most OS start well above 32768. See https://www.ncftp.com/ncftpd/doc/misc/ephemeral_ports.html
        private const int MinPort = 30000;
        private const int MaxPort = 31000;

        private readonly DurableTaskExtension extension;
        private readonly ILoggerFactory loggerFactory;
        private readonly IOrchestrationService orchestrationService;
        private readonly IOrchestrationServiceClient orchestrationServiceClient;

        private readonly Random portGenerator;
        private readonly HashSet<int> attemptedPorts;

        private IWebHost? host;

        public LocalGrpcListener(
            DurableTaskExtension extension,
            ILoggerFactory loggerFactory,
            IOrchestrationService orchestrationService,
            IOrchestrationServiceClient orchestrationServiceClient)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
            this.orchestrationServiceClient = orchestrationServiceClient ?? throw new ArgumentNullException(nameof(orchestrationServiceClient));

            this.portGenerator = new Random();
            this.attemptedPorts = new HashSet<int>();
        }

        public string? ListenAddress { get; private set; }

        public void Dispose()
        {
            this.host?.Dispose();
        }

        public async Task StartAsync(CancellationToken cancelToken = default)
        {
            const int maxAttempts = 10;
            int numAttempts = 1;
            while (numAttempts <= maxAttempts)
            {
                int listeningPort = numAttempts == 1 ? DefaultPort : this.GetRandomPort();
                string listenAddress = $"http://localhost:{listeningPort}";
                this.host?.Dispose();
                this.host = new WebHostBuilder()
                    .UseKestrel(options =>
                    {
                        // Need to force Http2 in Kestrel in unencrypted scenarios
                        // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0
                        options.ConfigureEndpointDefaults(listenOptions => listenOptions.Protocols = HttpProtocols.Http2);

                        // Ensure that there is no maximum request size since this is an internal endpoint
                        options.Limits.MaxRequestBodySize = null;
                        options.AddServerHeader = false;
                    })
                    .UseUrls(listenAddress)
                    .ConfigureServices(services =>
                    {
                        services.AddGrpc();
                        services.AddSingleton<ILoggerFactory>(this.loggerFactory);
                        services.AddSingleton<IOrchestrationService>(this.orchestrationService);
                        services.AddSingleton<IOrchestrationServiceClient>(this.orchestrationServiceClient);
                        services.AddSingleton<TaskHubGrpcServer>();

                        // Configure the server to run in API server-only mode (disables the dispatcher, which we don't use).
                        services.AddOptions<TaskHubGrpcServerOptions>().Configure(options => options.Mode = TaskHubGrpcServerMode.ApiServerOnly);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<TaskHubGrpcServer>();
                        });
                    })
                    .Build();

                try
                {
                    await this.host.StartAsync(cancelToken);
                    this.ListenAddress = listenAddress;

                    this.extension.TraceHelper.ExtensionInformationalEvent(
                        this.extension.Options.HubName,
                        instanceId: string.Empty,
                        functionName: string.Empty,
                        message: $"Opened local gRPC endpoint: {listenAddress}",
                        writeToUserLogs: true);

                    return;
                }
                catch (IOException)
                {
                    this.extension.TraceHelper.ExtensionWarningEvent(
                        this.extension.Options.HubName,
                        functionName: string.Empty,
                        instanceId: string.Empty,
                        message: $"Failed to open local port {listeningPort}. This was attempt #{numAttempts} to open a local port.");
                    this.attemptedPorts.Add(listeningPort);
                    numAttempts++;
                }
            }

            throw new IOException($"Unable to find a port to open an RPC endpoint on after {maxAttempts} attempts");
        }

        public Task StopAsync(CancellationToken cancelToken = default)
        {
            if (this.host != null && this.ListenAddress != null)
            {
                this.extension.TraceHelper.ExtensionInformationalEvent(
                    this.extension.Options.HubName,
                    instanceId: string.Empty,
                    functionName: string.Empty,
                    message: $"Closing local gRPC endpoint: {this.ListenAddress}",
                    writeToUserLogs: true);

                return this.host.StopAsync(cancelToken);
            }

            return Task.CompletedTask;
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
    }
}
#endif