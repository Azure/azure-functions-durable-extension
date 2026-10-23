// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.AzureBlobPayloads;
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
    }

    [Fact]
    public async Task InvalidHelperConfigurationDoesNotFailWorkerStartup()
    {
        StartProbeHostedService probe = new();
        ListLoggerProvider logs = new();
        using IHost host = CreateHost(
        services =>
        {
            services.AddLogging(builder => builder.AddProvider(logs));
            services.AddSingleton<IHostedService>(probe);
        },
        worker => worker.ConfigureLargePayloadPurgeFunctions(options =>
        {
            options.ContainerName = "Invalid_Name";
            options.ConnectionString = "not-a-storage-connection-string";
        }));

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(probe.Started);
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(nameof(PayloadStore), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThrowingPayloadStoreFactoryDoesNotRunDuringStartupBindingOrOrdinaryOperation()
    {
        StartProbeHostedService probe = new();
        int factoryCalls = 0;
        using IHost host = CreateHost(services =>
        {
            services.AddSingleton<IHostedService>(probe);
            services.AddSingleton<PayloadStore>(_ =>
            {
                factoryCalls++;
                throw new TestStoreFactoryException("custom registration failed");
            });
        });

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.Setup(client => client.GetInstancesAsync("instance", false, CancellationToken.None))
            .ReturnsAsync((OrchestrationMetadata?)null);
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);
        Assert.Null(await client.GetInstancesAsync("instance"));

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
    public void RepeatedStartupRegistrationAddsOneEnableReadinessValidator()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        IFunctionsWorkerApplicationBuilder worker = builder.Services.AddFunctionsWorkerCore();
        LargePayloadPurgeExtensionStartup startup = new();
        startup.Configure(worker);
        startup.Configure(worker);

        Assert.Equal(1, builder.Services.Count(
            descriptor => descriptor.ServiceType == typeof(ILargePayloadAutoPurgeReadinessValidator)));
        Assert.DoesNotContain(builder.Services,
            descriptor => descriptor.ImplementationType?.Name == "PayloadStoreValidation");
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

    [Fact]
    public async Task DisableWithMissingPayloadStoreSkipsValidationAndForwardsRpc()
    {
        using IHost host = CreateHost();
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(false, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        await ((ILargePayloadAutoPurgeClient)client)
            .SetLargePayloadAutoPurgeAsync(false, CancellationToken.None);

        inner.VerifyAll();
    }

    [Fact]
    public async Task EnableWithMissingPayloadStoreFailsBeforeRpc()
    {
        using IHost host = CreateHost();
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        Mock<ILargePayloadAutoPurgeClient> control = inner.As<ILargePayloadAutoPurgeClient>();
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SetLargePayloadAutoPurgeAsync(
                true, cancellationToken: CancellationToken.None));

        Assert.Contains(nameof(LargePayloadPurgeFunctionsExtensions.ConfigureLargePayloadPurgeFunctions), exception.Message);
        Assert.Contains("before enabling large-payload auto-purge", exception.Message);
        control.Verify(
            value => value.SetLargePayloadAutoPurgeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnableWithInvalidHelperConfigurationFailsBeforeRpc()
    {
        using IHost host = CreateHost(
            configureWorker: worker => worker.ConfigureLargePayloadPurgeFunctions(options =>
            {
                options.ContainerName = "Invalid_Name";
                options.ConnectionString = "not-a-storage-connection-string";
            }));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        Mock<ILargePayloadAutoPurgeClient> control = inner.As<ILargePayloadAutoPurgeClient>();
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ((ILargePayloadAutoPurgeClient)client)
                .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None));

        Assert.Contains(nameof(LargePayloadPurgeFunctionsExtensions.ConfigureLargePayloadPurgeFunctions), exception.Message);
        Assert.IsType<FormatException>(exception.InnerException);
        control.Verify(
            value => value.SetLargePayloadAutoPurgeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnablePropagatesUnexpectedPayloadStoreFactoryExceptionBeforeRpc()
    {
        TestStoreFactoryException failure = new("custom registration failed");
        using IHost host = CreateHost(services => services.AddSingleton<PayloadStore>(_ => throw failure));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        Mock<ILargePayloadAutoPurgeClient> control = inner.As<ILargePayloadAutoPurgeClient>();
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        Assert.Same(failure, await Assert.ThrowsAsync<TestStoreFactoryException>(
            () => ((ILargePayloadAutoPurgeClient)client)
                .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None)));
        control.Verify(
            value => value.SetLargePayloadAutoPurgeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnableCancellationDoesNotConstructPayloadStoreOrCallRpc()
    {
        int factoryCalls = 0;
        using IHost host = CreateHost(services => services.AddSingleton<PayloadStore>(_ =>
        {
            factoryCalls++;
            return new TrackingPayloadStore();
        }));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        Mock<ILargePayloadAutoPurgeClient> control = inner.As<ILargePayloadAutoPurgeClient>();
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ((ILargePayloadAutoPurgeClient)client)
                .SetLargePayloadAutoPurgeAsync(true, cancellation.Token));

        Assert.Equal(0, factoryCalls);
        control.Verify(
            value => value.SetLargePayloadAutoPurgeAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnableWithValidHelperConfigurationConstructsStoreThenForwardsRpc()
    {
        using IHost host = CreateHost(
            configureWorker: worker => worker.ConfigureLargePayloadPurgeFunctions(options =>
            {
                options.ConnectionString = "UseDevelopmentStorage=true";
                options.ContainerName = "durabletask-payloads";
            }));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        await ((ILargePayloadAutoPurgeClient)client)
            .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);

        inner.VerifyAll();
    }

    [Fact]
    public async Task RepeatedEnableUsesSingletonPayloadStoreWithoutDisposingIt()
    {
        TrackingPayloadStore store = new();
        int factoryCalls = 0;
        using IHost host = CreateHost(services => services.AddSingleton<PayloadStore>(_ =>
        {
            factoryCalls++;
            return store;
        }));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        await ((ILargePayloadAutoPurgeClient)client)
            .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);
        await ((ILargePayloadAutoPurgeClient)client)
            .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);

        Assert.Equal(1, factoryCalls);
        Assert.False(store.Disposed);
        inner.As<ILargePayloadAutoPurgeClient>().Verify(
            value => value.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None),
            Times.Exactly(2));
    }

    [Fact]
    public async Task EnableCreatesAndDisposesScopedPayloadStore()
    {
        List<TrackingPayloadStore> stores = [];
        using IHost host = CreateHost(services => services.AddScoped<PayloadStore>(_ =>
        {
            TrackingPayloadStore store = new();
            stores.Add(store);
            return store;
        }));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        await ((ILargePayloadAutoPurgeClient)client)
            .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);
        await ((ILargePayloadAutoPurgeClient)client)
            .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);

        Assert.Equal(2, stores.Count);
        Assert.All(stores, store => Assert.True(store.Disposed));
    }

    [Fact]
    public async Task RepeatedEnableAllowsAndDisposesAsyncOnlyScopedPayloadStoresAndDependencies()
    {
        List<AsyncOnlyPayloadStore> stores = [];
        List<AsyncOnlyDependency> dependencies = [];
        using IHost host = CreateHost(services =>
        {
            services.AddScoped(_ =>
            {
                AsyncOnlyDependency dependency = new();
                dependencies.Add(dependency);
                return dependency;
            });
            services.AddScoped<PayloadStore>(provider =>
            {
                AsyncOnlyPayloadStore store = new(provider.GetRequiredService<AsyncOnlyDependency>());
                stores.Add(store);
                return store;
            });
        });
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            await ((ILargePayloadAutoPurgeClient)client)
                .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);
        }

        Assert.Equal(2, stores.Count);
        Assert.All(stores, store => Assert.True(store.AsyncDisposed));
        Assert.Equal(2, dependencies.Count);
        Assert.All(dependencies, dependency => Assert.True(dependency.AsyncDisposed));
        inner.As<ILargePayloadAutoPurgeClient>().Verify(
            value => value.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None),
            Times.Exactly(2));
    }

    [Fact]
    public async Task EnableAllowsAsyncOnlySingletonPayloadStore()
    {
        AsyncOnlyPayloadStore store = new();
        using IHost host = CreateHost(services => services.AddSingleton<PayloadStore>(store));
        await host.StartAsync();
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(client => client.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None))
            .Returns(Task.CompletedTask);
        FunctionsDurableTaskClient client = CreateBoundClient(host, inner.Object);

        await ((ILargePayloadAutoPurgeClient)client)
            .SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);

        Assert.False(store.AsyncDisposed);
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
        new LargePayloadPurgeExtensionStartup().Configure(worker);
        return builder.Build();
    }

    private static FunctionsDurableTaskClient CreateBoundClient(IHost host, DurableTaskClient inner)
        => new(
            inner,
            queryString: null,
            httpBaseUrl: null,
            readinessValidator: host.Services.GetRequiredService<ILargePayloadAutoPurgeReadinessValidator>());

    private sealed class TrackingPayloadStore : PayloadStore, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            this.Disposed = true;
        }

        public override Task<string> UploadAsync(string payLoad, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override Task<string> DownloadAsync(string token, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override bool IsKnownPayloadToken(string value)
            => false;
    }

    private sealed class AsyncOnlyPayloadStore : PayloadStore, IAsyncDisposable
    {
        public AsyncOnlyPayloadStore(AsyncOnlyDependency? dependency = null)
        {
            _ = dependency;
        }

        public bool AsyncDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            this.AsyncDisposed = true;
            return default;
        }

        public override Task<string> UploadAsync(string payLoad, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override Task<string> DownloadAsync(string token, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override bool IsKnownPayloadToken(string value)
            => false;
    }

    private sealed class AsyncOnlyDependency : IAsyncDisposable
    {
        public bool AsyncDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            this.AsyncDisposed = true;
            return default;
        }
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

    private sealed class TestStoreFactoryException(string message) : Exception(message)
    {
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

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly List<string> entries = [];

        public IReadOnlyList<string> Entries => this.entries;

        public ILogger CreateLogger(string categoryName)
            => new ListLogger(this.entries);

        public void Dispose()
        {
        }

        private sealed class ListLogger(List<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel)
                => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Add(formatter(state, exception));
            }
        }
    }
}
