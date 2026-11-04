// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public sealed class LargePayloadPurgeStartupTests
{
    [Fact]
    public async Task MissingPayloadStoreDoesNotFailWorkerStartup()
    {
        StartProbeHostedService probe = new();
        using IHost host = CreateHost(services => services.AddSingleton<IHostedService>(probe));

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(probe.Started);
        Assert.Null(host.Services.GetService<PayloadStore>());
    }

    [Fact]
    public async Task InvalidHelperConfigurationDoesNotFailWorkerStartup()
    {
        StartProbeHostedService probe = new();
        using IHost host = CreateHost(
            services => services.AddSingleton<IHostedService>(probe),
            worker => worker.ConfigureLargePayloadPurgeFunctions(options =>
            {
                options.ContainerName = "Invalid_Name";
                options.ConnectionString = "not-a-storage-connection-string";
            }));

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(probe.Started);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton, false)]
    [InlineData(ServiceLifetime.Singleton, true)]
    [InlineData(ServiceLifetime.Scoped, false)]
    [InlineData(ServiceLifetime.Scoped, true)]
    [InlineData(ServiceLifetime.Transient, false)]
    [InlineData(ServiceLifetime.Transient, true)]
    public async Task ClientOperationsDoNotConstructPayloadStore(ServiceLifetime lifetime, bool enabled)
    {
        StartProbeHostedService probe = new();
        int factoryCalls = 0;
        using IHost host = CreateHost(services =>
        {
            services.AddSingleton<IHostedService>(probe);
            services.Add(new ServiceDescriptor(
                typeof(PayloadStore),
                _ =>
                {
                    factoryCalls++;
                    throw new InvalidOperationException("Client operations must not construct the store.");
                },
                lifetime));
        });

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.Setup(client => client.GetInstancesAsync("instance", false, CancellationToken.None))
            .ReturnsAsync((OrchestrationMetadata?)null);
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(enabled, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = new(inner.Object, null, null);
        Assert.Null(await client.GetInstancesAsync("instance"));
        await ((ILargePayloadAutoPurgeClient)client).SetLargePayloadAutoPurgeAsync(enabled, CancellationToken.None);

        Assert.True(probe.Started);
        Assert.Equal(0, factoryCalls);
        inner.VerifyAll();
    }

    [Fact]
    public async Task WorkerLoggingStartupDoesNotRequirePayloadStore()
    {
        using IHost host = CreateHost(services =>
        {
            services.AddLogging(builder =>
            {
                Type workerLoggerProvider = typeof(FunctionContext).Assembly.GetType(
                    "Microsoft.Azure.Functions.Worker.Logging.WorkerLoggerProvider", throwOnError: true)!;
                builder.Services.TryAddEnumerable(
                    ServiceDescriptor.Singleton(typeof(ILoggerProvider), workerLoggerProvider));
            });
        });

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));

        WorkerOptions workerOptions = host.Services.GetRequiredService<IOptions<WorkerOptions>>().Value;
        Assert.NotNull(workerOptions.Serializer);
    }

    [Fact]
    public async Task CallerCancellationStillPropagatesDuringUnrelatedHostStartup()
    {
        BlockingHostedService blocker = new();
        using IHost host = CreateHost(
            services => services.AddSingleton<IHostedService>(blocker),
            worker => worker.ConfigureLargePayloadPurgeFunctions(options =>
            {
                options.ConnectionString = "UseDevelopmentStorage=true";
                options.ContainerName = "durabletask-payloads";
            }));
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.StartAsync(cancellation.Token));

        Assert.False(blocker.Started);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnableAndDisableWithMissingPayloadStoreForwardToInnerClient(bool enabled)
    {
        using IHost host = CreateHost();
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(enabled, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = new(inner.Object, null, null);

        await ((ILargePayloadAutoPurgeClient)client).SetLargePayloadAutoPurgeAsync(enabled, CancellationToken.None);

        Assert.Null(host.Services.GetService<PayloadStore>());
        inner.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnableAndDisableWithInvalidStoreConfigurationForwardToInnerClient(bool enabled)
    {
        using IHost host = CreateHost(
            configureWorker: worker => worker.ConfigureLargePayloadPurgeFunctions(options =>
            {
                options.ContainerName = "Invalid_Name";
                options.ConnectionString = "not-a-storage-connection-string";
            }));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(enabled, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = new(inner.Object, null, null);

        await ((ILargePayloadAutoPurgeClient)client).SetLargePayloadAutoPurgeAsync(enabled, CancellationToken.None);

        inner.VerifyAll();
    }

    private static IHost CreateHost(
        Action<IServiceCollection>? configure = null,
        Action<IFunctionsWorkerApplicationBuilder>? configureWorker = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        IFunctionsWorkerApplicationBuilder worker = builder.Services.AddFunctionsWorkerCore();
        for (int index = builder.Services.Count - 1; index >= 0; index--)
        {
            if (builder.Services[index].ServiceType == typeof(IHostedService))
            {
                builder.Services.RemoveAt(index);
            }
        }

        configure?.Invoke(builder.Services);
        configureWorker?.Invoke(worker);
        return builder.Build();
    }

    private sealed class BlockingHostedService : IHostedService
    {
        public bool Started { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            this.Started = true;
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class StartProbeHostedService : IHostedService
    {
        public bool Started { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
