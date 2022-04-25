// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
#if !FUNCTIONS_V1
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Grpc.Core;
using Microsoft.DurableTask.Protobuf;
using Microsoft.DurableTask.Sidecar.Grpc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        private SimpleHostLifetime? lifetime;
        private Server? grpcServer;

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
            this.lifetime?.SignalShutdown();
            this.lifetime?.Dispose();
        }

        public Task StartAsync(CancellationToken cancelToken = default)
        {
            const int maxAttempts = 10;
            int numAttempts = 1;
            while (numAttempts <= maxAttempts)
            {
                int listeningPort = numAttempts == 1 ? DefaultPort : this.GetRandomPort();

                // Configure the server to run in API server-only mode (disables the dispatcher, which we don't use).
                var options = new TaskHubGrpcServerOptions { Mode = TaskHubGrpcServerMode.ApiServerOnly };

                this.lifetime?.Dispose();
                this.lifetime = new SimpleHostLifetime();
                this.grpcServer = new Server();
                this.grpcServer.Services.Add(TaskHubSidecarService.BindService(new TaskHubGrpcServer(
                    this.lifetime,
                    this.loggerFactory,
                    this.orchestrationService,
                    this.orchestrationServiceClient,
                    new OptionsWrapper<TaskHubGrpcServerOptions>(options))));

                int portBindingResult = this.grpcServer.Ports.Add("localhost", listeningPort, ServerCredentials.Insecure);
                if (portBindingResult != 0)
                {
                    try
                    {
                        this.grpcServer.Start();
                        this.ListenAddress = $"http://localhost:{listeningPort}";

                        this.extension.TraceHelper.ExtensionInformationalEvent(
                            this.extension.Options.HubName,
                            instanceId: string.Empty,
                            functionName: string.Empty,
                            message: $"Opened local gRPC endpoint: {this.ListenAddress}",
                            writeToUserLogs: true);

                        return Task.CompletedTask;
                    }
                    catch (IOException)
                    {
                        portBindingResult = 0;
                    }
                }

                if (portBindingResult == 0)
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

        public async Task StopAsync(CancellationToken cancelToken = default)
        {
            this.lifetime?.SignalShutdown();
            if (this.grpcServer != null)
            {
                await this.grpcServer.ShutdownAsync();
            }
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

        private sealed class SimpleHostLifetime : IHostApplicationLifetime, IDisposable
        {
            private readonly CancellationTokenSource startupSource = new CancellationTokenSource();
            private readonly CancellationTokenSource shutdownSource = new CancellationTokenSource();

            CancellationToken IHostApplicationLifetime.ApplicationStarted => this.startupSource.Token;

            CancellationToken IHostApplicationLifetime.ApplicationStopping => this.shutdownSource.Token;

            CancellationToken IHostApplicationLifetime.ApplicationStopped => this.shutdownSource.Token;

            public void Dispose()
            {
                this.startupSource.Dispose();
                this.shutdownSource.Dispose();
            }

            void IHostApplicationLifetime.StopApplication() => this.shutdownSource.Cancel();

            internal void SignalStarting() => this.startupSource.Cancel();

            internal void SignalShutdown() => this.shutdownSource.Cancel();
        }
    }
}
#endif