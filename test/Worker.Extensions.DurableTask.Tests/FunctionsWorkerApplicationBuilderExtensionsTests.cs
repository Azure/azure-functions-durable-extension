// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using DurableTask.Core;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Middleware;
using Microsoft.DurableTask.Worker.Shims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Tests;

public sealed class FunctionsWorkerApplicationBuilderExtensionsTests
{
    [Fact]
    public void ConfigureDurableExtension_RegisteredShimFactoryUsesOrchestrationMiddleware()
    {
        // Arrange
        ServiceCollection services = CreateServices();
        TestFunctionsWorkerApplicationBuilder builder = new(services);

        // Act
        builder.ConfigureDurableExtension();
        builder.ConfigureDurableWorker().UseOrchestrationMiddleware<CapturingOrchestrationMiddleware>();
        using ServiceProvider provider = services.BuildServiceProvider();
        DurableTaskShimFactory factory = provider.GetRequiredService<DurableTaskShimFactory>();

        // Assert
        Assert.True(factory.HasOrchestrationMiddleware);
    }

    [Fact]
    public async Task ConfigureDurableExtension_RegisteredShimFactoryUsesActivityMiddleware()
    {
        // Arrange
        CapturedActivityMiddleware captured = new();
        ServiceCollection services = CreateServices();
        services.AddSingleton(captured);
        TestFunctionsWorkerApplicationBuilder builder = new(services);
        builder.ConfigureDurableExtension();
        builder.ConfigureDurableWorker().UseActivityMiddleware<CapturingActivityMiddleware>();
        using ServiceProvider provider = services.BuildServiceProvider();
        DurableTaskShimFactory factory = provider.GetRequiredService<DurableTaskShimFactory>();
        using IServiceScope scope = provider.CreateScope();
        TaskActivity activity = factory.CreateActivity(
            "MiddlewareRegistrationActivity",
            FuncTaskActivity.Create<string, string>((context, input) => Task.FromResult("ok")),
            scope.ServiceProvider,
            new MiddlewareFeatureCollection());
        TaskContext taskContext = new(new OrchestrationInstance { InstanceId = "test-instance-id" });

        // Act
        await activity.RunAsync(taskContext, "\"input\"");

        // Assert
        Assert.True(captured.Called);
    }

    static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        return services;
    }

    sealed class TestFunctionsWorkerApplicationBuilder(IServiceCollection services) : IFunctionsWorkerApplicationBuilder
    {
        public IServiceCollection Services { get; } = services;

        public IFunctionsWorkerApplicationBuilder Use(
            Func<FunctionExecutionDelegate, FunctionExecutionDelegate> middleware)
        {
            return this;
        }
    }

    sealed class CapturingOrchestrationMiddleware : ITaskOrchestrationMiddleware
    {
        public Task InvokeAsync(
            TaskOrchestrationMiddlewareContext context,
            TaskOrchestrationMiddlewareDelegate next)
        {
            return next(context);
        }
    }

    sealed class CapturingActivityMiddleware(CapturedActivityMiddleware captured) : ITaskActivityMiddleware
    {
        public Task InvokeAsync(
            TaskActivityMiddlewareContext context,
            TaskActivityMiddlewareDelegate next)
        {
            captured.Called = true;
            return next(context);
        }
    }

    sealed class CapturedActivityMiddleware
    {
        public bool Called { get; set; }
    }
}
