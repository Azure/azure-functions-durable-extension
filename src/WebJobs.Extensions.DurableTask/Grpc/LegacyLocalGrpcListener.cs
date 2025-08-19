// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc
{
    internal class LegacyLocalGrpcListener : ILocalGrpcListener
    {
        private const int DefaultPort = 4001;

        // Pick a large, fixed range of ports that are going to be valid in all environment.
        // Avoiding ports below 1024 as those are blocked by app service sandbox.
        // Ephemeral ports for most OS start well above 32768. See https://www.ncftp.com/ncftpd/doc/misc/ephemeral_ports.html
        private const int MinPort = 30000;
        private const int MaxPort = 31000;

        private readonly DurableTaskExtension extension;

        private readonly Random portGenerator;
        private readonly HashSet<int> attemptedPorts;

        private Server? grpcServer;

        public LegacyLocalGrpcListener(DurableTaskExtension extension)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));

            this.portGenerator = new Random();
            this.attemptedPorts = new HashSet<int>();
        }

        public string? ListenAddress { get; private set; }

        public async Task StartAsync(CancellationToken cancelToken)
        {
            const int maxAttempts = 10;
            int numAttempts = 1;
            while (numAttempts <= maxAttempts)
            {
                ChannelOption[] options = new[]
                {
                    new ChannelOption(ChannelOptions.MaxReceiveMessageLength, int.MaxValue),
                    new ChannelOption(ChannelOptions.MaxSendMessageLength, int.MaxValue),
                };

                if (this.grpcServer is not null)
                {
                    try
                    {
                        await this.grpcServer.ShutdownAsync();
                    }
                    catch (IOException)
                    {
                        // Do nothing, IOException is a known exception type when trying to shutdown a server
                        // when its port was already in use
                    }
                    catch (Exception ex)
                    {
                        this.extension.TraceHelper.ExtensionWarningEvent(
                            this.extension.Options.HubName,
                            functionName: string.Empty,
                            instanceId: string.Empty,
                            message: $"Unexpected error when closing gRPC server. Exception details: {ex}");
                    }
                }

                this.grpcServer = new Server(options);
                this.grpcServer.Services.Add(P.TaskHubSidecarService.BindService(new TaskHubGrpcServer(this.extension)));

                // Attempt to get an unused port. Note that while unlikely, it is possible that the port returned by this method
                // may be utilized by another process between this call and the gRPC server create below, hence we still need to
                // guard against port conflicts.
                int listeningPort = this.GetAvailablePort();
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

                        return;
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

        public async Task StopAsync(CancellationToken cancelToken)
        {
            if (this.grpcServer != null)
            {
                await this.grpcServer.ShutdownAsync();
            }
        }

        private int GetAvailablePort()
        {
            // Get an available port for use in the gRPC server. Try 4001 first, then select a random open port
            // in the 30000-31000 range.
            if (this.IsTcpPortFree(DefaultPort))
            {
                return DefaultPort;
            }

            int numAttempts = 50;
            int randomPort;
            for (int i = 0; i < numAttempts; i++)
            {
                randomPort = this.portGenerator.Next(MinPort, MaxPort);
                if (this.IsTcpPortFree(randomPort))
                {
                    return randomPort;
                }
            }

            throw new InvalidOperationException($"Failed to get free port for local gRPC server after {numAttempts} attempts");
        }

        private bool IsTcpPortFree(int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                this.extension.TraceHelper.ExtensionWarningEvent(
                    this.extension.Options.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: $"Starting Durable gRPC server - Port {port} is already in use.");
                return false;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
