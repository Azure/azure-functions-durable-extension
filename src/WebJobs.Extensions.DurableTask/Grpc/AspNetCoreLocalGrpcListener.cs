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
        private readonly SemaphoreSlim startLock = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<string> addressReady = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private IHost? host;

        public AspNetCoreLocalGrpcListener(DurableTaskExtension extension)
        {
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
        }

        public string? ListenAddress { get; private set; }

        public bool IsHealthy
        {
            get
            {
                if (this.host is not { } currentHost)
                {
                    return false;
                }

                // Check if the Kestrel server is still accepting connections by verifying
                // that the IServer is still reporting addresses.
                try
                {
                    IServer? server = currentHost.Services.GetService<IServer>();
                    IServerAddressesFeature? addressFeature = server?.Features.Get<IServerAddressesFeature>();
                    return addressFeature?.Addresses.Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Fast check: if already started and healthy, return immediately
            if (this.host != null && this.IsHealthy)
            {
                return;
            }

            await this.startLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring the lock
                if (this.host == null || !this.IsHealthy)
                {
                    await this.StopInternalAsync(cancellationToken);
                    await this.StartInternalAsync(cancellationToken);
                }
            }
            finally
            {
                this.startLock.Release();
            }
        }

        public async Task EnsureStartedAsync(CancellationToken cancellationToken)
        {
            if (this.host != null && this.IsHealthy && this.ListenAddress != null)
            {
                return;
            }

            this.extension.TraceHelper.ExtensionWarningEvent(
                this.extension.Options.HubName,
                instanceId: string.Empty,
                functionName: string.Empty,
                message: $"gRPC listener is not healthy (ListenAddress={this.ListenAddress ?? "null"}, " +
                         $"host={(this.host != null ? "exists" : "null")}, IsHealthy={this.IsHealthy}). " +
                         $"Attempting restart.");

            // Delegate to StartAsync which handles locking, health check, and restart
            await this.StartAsync(cancellationToken);
        }

        public async Task<string?> WaitForListenAddressAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            // If already available, return immediately
            if (this.ListenAddress != null)
            {
                return this.ListenAddress;
            }

            // Wait for the TaskCompletionSource to be signaled, with a timeout.
            // Task.WhenAny does not throw when the delay is canceled, so we check
            // which task completed to distinguish success from timeout.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            Task<string> addressTask = this.addressReady.Task;
            Task delayTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
            Task completedTask = await Task.WhenAny(addressTask, delayTask);

            if (completedTask == addressTask)
            {
                return await addressTask;
            }

            // Timeout or cancellation — return null rather than reading ListenAddress,
            // which could be set concurrently and produce unpredictable results.
            return null;
        }

        private async Task StartInternalAsync(CancellationToken cancellationToken)
        {
            int port = GetFreeTcpPort();
            IHost newHost = new HostBuilder().ConfigureWebHost(
                builder =>
                {
                    builder.UseKestrel(o => o.Listen(
                        IPAddress.Parse(HostName),
                        port,
                        listenOptions => listenOptions.Protocols = HttpProtocols.Http2));

                    builder.ConfigureServices(services =>
                    {
                        services.AddSingleton(this.extension);

                        // Register the interceptor in DI so DefaultGrpcInterceptorActivator resolves
                        // this singleton instead of constructing a new instance for every RPC.
                        services.AddSingleton<TaskHubGrpcExceptionInterceptor>();
                        services.AddGrpc(options =>
                        {
                            options.Interceptors.Add<TaskHubGrpcExceptionInterceptor>();
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

            await newHost.StartAsync(cancellationToken);

            // Get the actual address we've started on.
            IServer? server = newHost.Services.GetService<IServer>();
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

            // Assign only after fully started so the fast-path check in StartAsync
            // doesn't let concurrent callers return before initialization is complete.
            this.host = newHost;

            // Signal any waiters that the address is now available.
            // If ListenAddress is null (e.g. IServerAddressesFeature returned no addresses),
            // stop the host to avoid a partially-initialized state that would cause
            // repeated stop/restart loops from the IsHealthy check.
            if (this.ListenAddress != null)
            {
                this.addressReady.TrySetResult(this.ListenAddress);
            }
            else
            {
                this.extension.TraceHelper.ExtensionWarningEvent(
                    this.extension.Options.HubName,
                    instanceId: string.Empty,
                    functionName: string.Empty,
                    message: "gRPC listener started but ListenAddress is null. Stopping to allow retry.");

                // Roll back: stop the host so the next StartAsync call can try again
                // rather than leaving the listener in a broken state.
                await this.StopInternalAsync();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await this.startLock.WaitAsync(cancellationToken);
            try
            {
                await this.StopInternalAsync(cancellationToken);
            }
            finally
            {
                this.startLock.Release();
            }
        }

        private async Task StopInternalAsync(CancellationToken cancellationToken = default)
        {
            if (this.host is { } previousHost)
            {
                try
                {
                    await previousHost.StopAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    this.extension.TraceHelper.ExtensionWarningEvent(
                        this.extension.Options.HubName,
                        instanceId: string.Empty,
                        functionName: string.Empty,
                        message: $"Error stopping local gRPC endpoint: {ex.Message}");
                }
                finally
                {
                    // Dispose the host to release server, sockets, and DI container resources.
                    // This prevents resource leaks during repeated stop/start cycles.
                    if (previousHost is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                // Reset state so StartAsync can re-initialize.
                // Re-create the TCS so that WaitForListenAddressAsync callers
                // don't receive a stale address from a previous start cycle.
                this.host = null;
                this.ListenAddress = null;
                this.addressReady = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
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
