// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.Azure.Functions.Worker.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class LargePayloadPurgeStartupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalPackageWithoutHelperOrStoreFailsBeforeHostedWorkerStarts(bool concurrentStart)
    {
        var worker = new StartProbe();
        using IHost host = CreateHost(worker, (_, _) => { }, concurrentStart: concurrentStart);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("ConfigureLargePayloadPurgeFunctions", exception.ToString());
        Assert.False(worker.Started);
    }

    [Fact]
    public async Task InvalidHelperConfigurationFailsBeforeHostedWorkerStarts()
    {
        var worker = new StartProbe();
        using IHost host = CreateHost(worker, (builder, _) => builder.ConfigureLargePayloadPurgeFunctions(_ => { }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("ConfigureLargePayloadPurgeFunctions", exception.Message);
        Assert.NotNull(exception.InnerException);
        Assert.False(worker.Started);
    }

    [Fact]
    public async Task StoreFactoryFailureRetainsCauseAndPreventsWorkerStart()
    {
        var worker = new StartProbe();
        var failure = new ArgumentException("Invalid custom store configuration.");
        using IHost host = CreateHost(worker, (_, services) => services.AddSingleton<PayloadStore>(_ => throw failure));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Same(failure, exception.InnerException);
        Assert.Contains("PayloadStore", exception.Message);
        Assert.False(worker.Started);
    }

    [Fact]
    public async Task UnexpectedStoreFactoryFailurePropagatesUnchanged()
    {
        var worker = new StartProbe();
        var failure = new NullReferenceException("Unexpected custom factory bug.");
        using IHost host = CreateHost(worker, (_, services) => services.AddSingleton<PayloadStore>(_ => throw failure));

        Assert.Same(failure, await Assert.ThrowsAsync<NullReferenceException>(() => host.StartAsync()));
        Assert.False(worker.Started);
    }

    [Fact]
    public async Task ValidHelperConfigurationStartsWithoutStorageNetworkProbe()
    {
        var worker = new StartProbe();
        using IHost host = CreateHost(worker, (builder, _) => builder.ConfigureLargePayloadPurgeFunctions(options =>
            options.ConnectionString = "UseDevelopmentStorage=true"));

        await host.StartAsync();
        Assert.True(worker.Started);
        await host.StopAsync();
    }

    [Fact]
    public async Task CustomSingletonStoreWithoutHelperIsValidatedButNotDisposedByValidationScope()
    {
        int created = 0;
        int disposed = 0;
        var store = new Mock<PayloadStore>();
        store.As<IDisposable>().Setup(value => value.Dispose()).Callback(() => disposed++);
        var worker = new StartProbe();
        using (IHost host = CreateHost(worker, (_, services) => services.AddSingleton<PayloadStore>(_ =>
        {
            created++;
            return store.Object;
        })))
        {
            await host.StartAsync();
            Assert.True(worker.Started);
            Assert.Equal(1, created);
            Assert.Equal(0, disposed);
            await host.StopAsync();
        }

        Assert.Equal(1, disposed);
    }

    [Fact]
    public async Task ScopedCustomStoreIsValidatedInItsOwnScope()
    {
        int created = 0;
        int disposed = 0;
        var worker = new StartProbe();
        using IHost host = CreateHost(worker, (_, services) => services.AddScoped<PayloadStore>(_ =>
        {
            created++;
            var store = new Mock<PayloadStore>();
            store.As<IDisposable>().Setup(value => value.Dispose()).Callback(() => disposed++);
            return store.Object;
        }));

        await host.StartAsync();
        Assert.True(worker.Started);
        Assert.Equal(1, created);
        Assert.Equal(1, disposed);
        await host.StopAsync();
    }

    [Fact]
    public async Task BaseOnlyWorkerWithoutStoreStillStarts()
    {
        var worker = new StartProbe();
        using IHost host = CreateHost(worker, (_, _) => { }, includeOptionalStartup: false);

        await host.StartAsync();
        Assert.True(worker.Started);
        await host.StopAsync();
    }

    [Theory]
    [InlineData("Fetch")]
    [InlineData("Report")]
    public void NonStorageActivitiesActivateWithoutPayloadStore(string methodName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFunctionsWorkerCore();
        using ServiceProvider provider = services.BuildServiceProvider();
        var context = new Mock<FunctionContext>();
        context.SetupGet(value => value.InstanceServices).Returns(provider);
        MethodInfo method = GetPurgeMethods().Single(value => value.Name == methodName);

        object? instance = provider.GetRequiredService<IFunctionActivator>().CreateInstance(method.DeclaringType!, context.Object);

        Assert.NotNull(instance);
        Assert.Null(provider.GetService<PayloadStore>());
    }

    [Fact]
    public void DeleteActivityStillRequiresPayloadStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFunctionsWorkerCore();
        using ServiceProvider provider = services.BuildServiceProvider();
        var context = new Mock<FunctionContext>();
        context.SetupGet(value => value.InstanceServices).Returns(provider);
        MethodInfo method = GetPurgeMethods().Single(value => value.Name == "Delete");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IFunctionActivator>().CreateInstance(method.DeclaringType!, context.Object));

        Assert.Contains(nameof(PayloadStore), exception.Message);
    }

    private static IEnumerable<MethodInfo> GetPurgeMethods() => typeof(LargePayloadPurgeFunctions).Assembly.GetTypes()
        .SelectMany(type => type.GetMethods()).Where(method => method.GetCustomAttribute<FunctionAttribute>() != null);

    private static IHost CreateHost(
        StartProbe worker,
        Action<IFunctionsWorkerApplicationBuilder, IServiceCollection> configure,
        bool concurrentStart = false,
        bool includeOptionalStartup = true)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                IFunctionsWorkerApplicationBuilder builder = services.AddFunctionsWorkerCore();
                // Exercise the real Generic Host lifecycle without starting a gRPC worker connection.
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IHostedService>(worker);
                services.Configure<HostOptions>(options => options.ServicesStartConcurrently = concurrentStart);
                if (includeOptionalStartup)
                {
                    foreach (CustomAttributeData attribute in typeof(LargePayloadPurgeFunctions).Assembly.GetCustomAttributesData()
                        .Where(value => value.AttributeType == typeof(WorkerExtensionStartupAttribute)))
                    {
                        var startup = (WorkerExtensionStartup)Activator.CreateInstance((Type)attribute.ConstructorArguments[0].Value!)!;
                        startup.Configure(builder);
                    }
                }

                configure(builder, services);
            })
            .Build();
    }

    private sealed class StartProbe : IHostedService
    {
        public bool Started { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
