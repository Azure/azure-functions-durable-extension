// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class TaskOrchestrationContextExtensionMethodsTests
{
    [Fact]
    public void GetFunctionContext_WithNullContext_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TaskOrchestrationContextExtensionMethods.GetFunctionContext(null!));
    }

    [Fact]
    public void GetFunctionContext_WithFunctionsOrchestrationContext_ShouldReturnFunctionContext()
    {
        FunctionContext expectedFunctionContext = CreateMockFunctionContext();
        TaskOrchestrationContext innerContext = CreateMockTaskOrchestrationContext();
        FunctionsOrchestrationContext functionsContext = CreateFunctionsOrchestrationContext(innerContext, expectedFunctionContext);

        FunctionContext? result = functionsContext.GetFunctionContext();

        Assert.NotNull(result);
        Assert.Same(expectedFunctionContext, result);
    }

    [Fact]
    public void GetFunctionContext_WithNonFunctionsOrchestrationContext_ShouldReturnNull()
    {
        TaskOrchestrationContext context = CreateMockTaskOrchestrationContext();

        FunctionContext? result = context.GetFunctionContext();

        Assert.Null(result);
    }

    private static FunctionContext CreateMockFunctionContext()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        serviceCollection.AddSingleton(Options.Create(new DurableTaskWorkerOptions()));
        serviceCollection.AddSingleton(Options.Create(new JsonSerializerOptions()));
        IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        var mockFunctionContext = new Mock<FunctionContext>();
        mockFunctionContext.Setup(c => c.InstanceServices).Returns(serviceProvider);

        return mockFunctionContext.Object;
    }

    private static TaskOrchestrationContext CreateMockTaskOrchestrationContext()
    {
        var mockContext = new Mock<TaskOrchestrationContext>();
        mockContext.Setup(c => c.Name).Returns(new TaskName("TestOrchestration"));
        mockContext.Setup(c => c.InstanceId).Returns("test-instance-id");
        mockContext.Setup(c => c.CurrentUtcDateTime).Returns(DateTime.UtcNow);
        mockContext.Setup(c => c.IsReplaying).Returns(false);
        mockContext.Setup(c => c.Properties).Returns(new Dictionary<string, object?>());

        return mockContext.Object;
    }

    private static FunctionsOrchestrationContext CreateFunctionsOrchestrationContext(
        TaskOrchestrationContext innerContext,
        FunctionContext functionContext)
    {
        return new FunctionsOrchestrationContext(innerContext, functionContext);
    }
}
