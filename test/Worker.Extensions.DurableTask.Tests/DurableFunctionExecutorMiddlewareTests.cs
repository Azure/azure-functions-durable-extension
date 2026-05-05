// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Middleware;
using Microsoft.DurableTask.Worker.Shims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Tests;

public sealed class DurableFunctionExecutorMiddlewareTests
{
    [Fact]
    public async Task RunDirectActivityAsync_WithMiddleware_PopulatesFunctionContextAndReturnsRawResult()
    {
        // Arrange
        const string activityName = "MiddlewareContextActivity";
        object expected = new { Message = "raw-result" };
        CapturedActivityMiddleware captured = new();
        ServiceCollection services = new();
        services.AddSingleton(captured);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        IDurableTaskWorkerBuilder workerBuilder = services.AddDurableTaskWorker();
        workerBuilder.UseActivityMiddleware<CapturingActivityMiddleware>();
        using ServiceProvider provider = services.BuildServiceProvider();
        FunctionContext functionContext = new TestFunctionContext(activityName, provider);
        DurableTaskShimFactory shimFactory = new(workerBuilder.Name, provider, options: null, NullLoggerFactory.Instance);
        DurableFunctionExecutor executor = new(
            Mock.Of<IFunctionExecutor>(),
            new ExtendedSessionsCache(),
            new TestDurableTaskFactory(
                activityName,
                FuncTaskActivity.Create<string, object?>((context, input) => Task.FromResult<object?>(expected))),
            shimFactory,
            Options.Create(new DurableTaskWorkerOptions()));

        // Act
        object? result = await executor.RunDirectActivityAsync(functionContext, "\"input\"");

        // Assert
        Assert.Same(expected, result);
        Assert.Same(functionContext, captured.FunctionContext);
        Assert.Equal("input", captured.Input);
        Assert.Same(expected, captured.Result);
    }

    [Fact]
    public async Task RunFunctionActivityAsync_WithMiddleware_PopulatesFunctionContextAndReturnsFunctionResult()
    {
        // Arrange
        const string activityName = "FunctionSyntaxActivity";
        object expected = new { Message = "function-result" };
        CapturedActivityMiddleware captured = new();
        ServiceCollection services = new();
        services.AddSingleton(captured);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        IDurableTaskWorkerBuilder workerBuilder = services.AddDurableTaskWorker();
        workerBuilder.UseActivityMiddleware<CapturingActivityMiddleware>();
        using ServiceProvider provider = services.BuildServiceProvider();
        FunctionContext functionContext = new TestFunctionContext(activityName, provider);
        DurableTaskShimFactory shimFactory = new(workerBuilder.Name, provider, options: null, NullLoggerFactory.Instance);
        DurableFunctionExecutor executor = new(
            Mock.Of<IFunctionExecutor>(),
            new ExtendedSessionsCache(),
            new TestDurableTaskFactory(activityName, activity: null),
            shimFactory,
            Options.Create(new DurableTaskWorkerOptions()));
        FunctionActivityInput input = new(typeof(string), "input", "\"input\"");

        // Act
        object? result = await executor.RunFunctionActivityAsync(
            functionContext,
            input,
            () => Task.FromResult<object?>(expected));

        // Assert
        Assert.Same(expected, result);
        Assert.Same(functionContext, captured.FunctionContext);
        Assert.Equal("input", captured.Input);
        Assert.Same(expected, captured.Result);
    }

    [Fact]
    public async Task RunFunctionActivityAsync_WhenMiddlewareSetsResult_SkipsFunctionBody()
    {
        // Arrange
        const string activityName = "ShortCircuitFunctionSyntaxActivity";
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        IDurableTaskWorkerBuilder workerBuilder = services.AddDurableTaskWorker();
        workerBuilder.UseActivityMiddleware((context, next) =>
        {
            context.SetResult("short-circuited");
            return Task.CompletedTask;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        FunctionContext functionContext = new TestFunctionContext(activityName, provider);
        DurableTaskShimFactory shimFactory = new(workerBuilder.Name, provider, options: null, NullLoggerFactory.Instance);
        DurableFunctionExecutor executor = new(
            Mock.Of<IFunctionExecutor>(),
            new ExtendedSessionsCache(),
            new TestDurableTaskFactory(activityName, activity: null),
            shimFactory,
            Options.Create(new DurableTaskWorkerOptions()));
        FunctionActivityInput input = new(typeof(string), "input", "\"input\"");
        bool bodyCalled = false;

        // Act
        object? result = await executor.RunFunctionActivityAsync(
            functionContext,
            input,
            () =>
            {
                bodyCalled = true;
                return Task.FromResult<object?>("body-result");
            });

        // Assert
        Assert.Equal("short-circuited", result);
        Assert.False(bodyCalled);
    }

    [Fact]
    public void CreateFunctionActivityInput_WithNullActivityInput_ReturnsSerializedNullRawInput()
    {
        // Arrange
        DataConverter converter = new DurableTaskWorkerOptions().DataConverter;

        // Act
        FunctionActivityInput input = DurableFunctionExecutor.CreateFunctionActivityInput(
            typeof(string),
            hasInput: true,
            input: null,
            converter);

        // Assert
        Assert.Equal(typeof(string), input.InputType);
        Assert.Null(input.Input);
        Assert.Equal("null", input.RawInput);
    }

    sealed class CapturingActivityMiddleware(CapturedActivityMiddleware captured) : ITaskActivityMiddleware
    {
        public async Task InvokeAsync(TaskActivityMiddlewareContext context, TaskActivityMiddlewareDelegate next)
        {
            captured.FunctionContext = context.GetFunctionContext();
            captured.Input = context.Input;

            await next(context);

            captured.Result = context.Result;
        }
    }

    sealed class CapturedActivityMiddleware
    {
        public FunctionContext? FunctionContext { get; set; }

        public object? Input { get; set; }

        public object? Result { get; set; }
    }

    sealed class TestDurableTaskFactory(TaskName activityName, ITaskActivity? activity) : IDurableTaskFactory
    {
        public bool TryCreateActivity(
            TaskName name,
            IServiceProvider serviceProvider,
            [NotNullWhen(true)] out ITaskActivity? result)
        {
            if (activity is not null && name == activityName)
            {
                result = activity;
                return true;
            }

            result = null;
            return false;
        }

        public bool TryCreateOrchestrator(
            TaskName name,
            IServiceProvider serviceProvider,
            [NotNullWhen(true)] out ITaskOrchestrator? orchestrator)
        {
            orchestrator = null;
            return false;
        }
    }

    sealed class TestFunctionContext(string functionName, IServiceProvider services) : FunctionContext
    {
        public override string InvocationId => throw new NotImplementedException();

        public override string FunctionId => throw new NotImplementedException();

        public override TraceContext TraceContext => throw new NotImplementedException();

        public override BindingContext BindingContext { get; } = new TestBindingContext();

        public override RetryContext RetryContext => throw new NotImplementedException();

        public override IServiceProvider InstanceServices { get; set; } = services;

        public override FunctionDefinition FunctionDefinition { get; } = new TestFunctionDefinition(functionName);

        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

        public override IInvocationFeatures Features => throw new NotImplementedException();
    }

    sealed class TestFunctionDefinition(string functionName) : FunctionDefinition
    {
        public override ImmutableArray<FunctionParameter> Parameters => ImmutableArray<FunctionParameter>.Empty;

        public override string PathToAssembly => throw new NotImplementedException();

        public override string EntryPoint => DurableFunctionExecutor.ActivityEntryPoint;

        public override string Id => throw new NotImplementedException();

        public override string Name => functionName;

        public override IImmutableDictionary<string, BindingMetadata> InputBindings =>
            ImmutableDictionary<string, BindingMetadata>.Empty;

        public override IImmutableDictionary<string, BindingMetadata> OutputBindings =>
            ImmutableDictionary<string, BindingMetadata>.Empty;
    }

    sealed class TestBindingContext : BindingContext
    {
        public override IReadOnlyDictionary<string, object?> BindingData { get; } =
            new Dictionary<string, object?> { ["instanceId"] = "test-instance-id" };
    }
}
