// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc
{
    internal class AspNetCoreLocalGrpcListener : ILocalGrpcListener
    {
        private const string HostName = "127.0.0.1";
        private readonly DurableTaskExtension extension;
        private IHost? host;

        public AspNetCoreLocalGrpcListener(DurableTaskExtension extension)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
        }

        public string? ListenAddress { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int port = GetFreeTcpPort();
            this.host = new HostBuilder().ConfigureWebHost(
                builder =>
                {
                    builder.UseKestrel(o => o.Listen(
                        IPAddress.Parse(HostName),
                        port,
                        listenOptions => listenOptions.Protocols = HttpProtocols.Http2));

                    builder.ConfigureServices(services =>
                    {
                        services.AddSingleton(this.extension);
                        services.AddGrpc(options =>
                        {
                            options.MaxReceiveMessageSize = int.MaxValue;
                            options.MaxSendMessageSize = int.MaxValue;
                        });
                    });

                    builder.Configure((context, app) =>
                    {
                        if (context.HostingEnvironment.IsDevelopment())
                        {
                            app.UseDeveloperExceptionPage();
                        }

                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<TaskHubGrpcServer>();
                        });
                    });
                })
                .Build();

            await this.host.StartAsync(cancellationToken);

            // Get the actual address we've started on.
            IServer? server = this.host.Services.GetService<IServer>();
            IServerAddressesFeature? addressFeature = server?.Features.Get<IServerAddressesFeature>();
            this.ListenAddress = addressFeature?.Addresses.SingleOrDefault();

            var expected = new Uri($"http://{HostName}:{port}");
            if (!Uri.TryCreate(this.ListenAddress, UriKind.Absolute, out Uri? uri) || expected != uri)
            {
                this.extension.TraceHelper.ExtensionWarningEvent(
                    this.extension.Options.HubName,
                    instanceId: string.Empty,
                    functionName: string.Empty,
                    message: $"Configured Uri ({expected}) does not match actual Uri ({uri}).");
            }

            this.extension.TraceHelper.ExtensionInformationalEvent(
                this.extension.Options.HubName,
                instanceId: string.Empty,
                functionName: string.Empty,
                message: $"Opened local gRPC endpoint: {this.ListenAddress} (Mode=AspNetCore)",
                writeToUserLogs: true);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (this.host is { } host)
            {
                return host.StopAsync(cancellationToken);
            }

            return Task.CompletedTask;
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
