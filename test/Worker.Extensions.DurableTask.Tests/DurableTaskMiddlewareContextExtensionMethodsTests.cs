// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker.Middleware;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Tests;

public sealed class DurableTaskMiddlewareContextExtensionMethodsTests
{
    [Fact]
    public void CreateMiddlewareFeatures_ShouldPopulateFunctionContextFeature()
    {
        // Arrange
        FunctionContext functionContext = Mock.Of<FunctionContext>();

        // Act
        IMiddlewareFeatures features = DurableFunctionExecutor.CreateMiddlewareFeatures(functionContext);

        // Assert
        Assert.Same(functionContext, features.Get<FunctionContext>());
    }

    [Fact]
    public void OrchestrationGetFunctionContext_WithNullContext_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DurableTaskMiddlewareContextExtensionMethods.GetFunctionContext((TaskOrchestrationMiddlewareContext)null!));
    }

    [Fact]
    public void OrchestrationGetFunctionContext_ShouldReturnFunctionContextFeature()
    {
        // Arrange
        FunctionContext expected = Mock.Of<FunctionContext>();
        MiddlewareFeatureCollection features = new();
        features.Set(expected);
        TaskOrchestrationMiddlewareContext context = new TestOrchestrationMiddlewareContext(features);

        // Act
        FunctionContext? result = context.GetFunctionContext();

        // Assert
        Assert.Same(expected, result);
    }

    [Fact]
    public void OrchestrationGetFunctionContext_WithoutFeature_ShouldReturnNull()
    {
        // Arrange
        TaskOrchestrationMiddlewareContext context = new TestOrchestrationMiddlewareContext(
            new MiddlewareFeatureCollection());

        // Act
        FunctionContext? result = context.GetFunctionContext();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ActivityGetFunctionContext_WithNullContext_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DurableTaskMiddlewareContextExtensionMethods.GetFunctionContext((TaskActivityMiddlewareContext)null!));
    }

    [Fact]
    public void ActivityGetFunctionContext_ShouldReturnFunctionContextFeature()
    {
        // Arrange
        FunctionContext expected = Mock.Of<FunctionContext>();
        MiddlewareFeatureCollection features = new();
        features.Set(expected);
        TaskActivityMiddlewareContext context = new MiddlewareActivityContext(features);

        // Act
        FunctionContext? result = context.GetFunctionContext();

        // Assert
        Assert.Same(expected, result);
    }

    [Fact]
    public void ActivityGetFunctionContext_WithoutFeature_ShouldReturnNull()
    {
        // Arrange
        TaskActivityMiddlewareContext context = new MiddlewareActivityContext(new MiddlewareFeatureCollection());

        // Act
        FunctionContext? result = context.GetFunctionContext();

        // Assert
        Assert.Null(result);
    }

    sealed class TestOrchestrationMiddlewareContext(IMiddlewareFeatures features)
        : TaskOrchestrationMiddlewareContext
    {
        public override TaskName Name => "TestOrchestration";

        public override string InstanceId => "test-instance-id";

        public override string Version => string.Empty;

        public override ParentOrchestrationInstance? Parent => null;

        public override IReadOnlyDictionary<string, string>? Tags => null;

        public override bool IsReplaying => false;

        public override Type InputType => typeof(string);

        public override object? Input => null;

        public override string? RawInput => null;

        public override TaskOrchestrationContext OrchestrationContext => Mock.Of<TaskOrchestrationContext>();

        public override IMiddlewareFeatures Features { get; } = features;

        public override CancellationToken CancellationToken => CancellationToken.None;

        public override object? Result => null;
    }

    sealed class MiddlewareActivityContext(IMiddlewareFeatures features)
        : TaskActivityMiddlewareContext
    {
        object? result;

        public override TaskName Name => "MiddlewareContextFunctionActivity";

        public override string InstanceId => "test-instance-id";

        public override Type InputType => typeof(string);

        public override object? Input => null;

        public override string? RawInput => null;

        public override TaskActivityContext ActivityContext => Mock.Of<TaskActivityContext>();

        public override IMiddlewareFeatures Features { get; } = features;

        public override IServiceProvider Services => Mock.Of<IServiceProvider>();

        public override CancellationToken CancellationToken => CancellationToken.None;

        public override object? Result => this.result;

        public override void SetResult(object? result)
        {
            this.result = result;
        }
    }
}
