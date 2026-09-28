// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections;
using System.Reflection;
using DurableTask.GeneratedInvocationApp;
using Microsoft.Azure.Functions.Worker.Context.Features;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.DurableTask;
using Microsoft.DurableTask.AzureBlobPayloads;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class GeneratedPurgeFunctionInvocationTests
{
    [Fact]
    public async Task GeneratedOrchestratorExecutesFiveCyclesAndContinuesWithPreservedState()
    {
        var input = new BlobPurgeJobRunRequest(17, 9);
        var context = new Mock<TaskOrchestrationContext>(MockBehavior.Strict);
        context.Setup(value => value.CreateReplaySafeLogger<BlobPurgeJobOrchestrator>()).Returns(NullLogger.Instance);
        context.Setup(value => value.GetInput<BlobPurgeJobRunRequest>()).Returns(input);
        context.SetupGet(value => value.CurrentUtcDateTime).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        context.SetupGet(value => value.InstanceId).Returns("purge-instance");
        context.SetupGet(value => value.IsReplaying).Returns(false);
        var configuration = new TaskCompletionSource<int>();
        context.Setup(value => value.WaitForExternalEvent<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(configuration.Task);
        context.Setup(value => value.CallActivityAsync<List<LargePayloadTombstone>>(
            new TaskName(nameof(GetLargePayloadTombstonesActivity)), 17, It.IsAny<TaskOptions>()))
            .ReturnsAsync(new List<LargePayloadTombstone>());
        context.Setup(value => value.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Setup(value => value.CreateTimer(TimeSpan.FromMinutes(1), It.IsAny<CancellationToken>())).CallBase();
        context.Setup(value => value.SetCustomStatus(It.IsAny<object>()));
        context.Setup(value => value.ContinueAsNew(It.IsAny<object>(), true));
        using ServiceProvider services = CreateServices();

        object? result = await Invoke(services, typeof(LargePayloadPurgeFunctions), nameof(LargePayloadPurgeFunctions.RunOrchestrator), context.Object);

        Assert.Null(result);
        context.Verify(value => value.GetInput<BlobPurgeJobRunRequest>(), Times.Once);
        context.Verify(value => value.CallActivityAsync<List<LargePayloadTombstone>>(
            new TaskName(nameof(GetLargePayloadTombstonesActivity)), 17, It.IsAny<TaskOptions>()), Times.Exactly(5));
        context.Verify(value => value.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        context.Verify(value => value.ContinueAsNew(
            It.Is<BlobPurgeJobRunRequest>(state => state.PurgeBatchSize == 17 && state.PurgedCount == 9), true), Times.Once);
    }

    [Fact]
    public async Task GeneratedFetchUsesBoundClientAndReturnsItsOpaqueTokens()
    {
        var purge = new Mock<ILargePayloadPurgeClient>(MockBehavior.Strict);
        var tombstones = new List<LargePayloadTombstone> { new("opaque:/+==", "blob:v2:https://account/container/payload") };
        DateTime before = DateTime.UtcNow;
        purge.Setup(value => value.GetLargePayloadTombstonesAsync(17, It.IsAny<DateTime>(), default))
            .ReturnsAsync(tombstones);
        using ServiceProvider services = CreateServices();

        object? result = await Invoke(services, typeof(LargePayloadPurgeFunctions), nameof(LargePayloadPurgeFunctions.Fetch),
            17, "purge-instance", BoundClient(purge.Object));

        Assert.Same(tombstones, result);
        Assert.Equal("opaque:/+==", Assert.Single(Assert.IsType<List<LargePayloadTombstone>>(result)).TombstoneToken);
        var call = Assert.Single(purge.Invocations);
        Assert.InRange(Assert.IsType<DateTime>(call.Arguments[1]), before, DateTime.UtcNow.AddMinutes(2));
        purge.VerifyAll();
    }

    [Fact]
    public async Task GeneratedDeleteActivatesStoreAndPreservesPositionalOutcomes()
    {
        var store = new Mock<PayloadStore>(MockBehavior.Strict);
        var tokens = new List<string> { "blob:v2:https://account/container/deleted", "blob:v2:https://account/container/retry" };
        store.Setup(value => value.DeleteAsync(tokens[0], It.Is<CancellationToken>(token => token.CanBeCanceled)))
            .ReturnsAsync(PayloadDeleteOutcome.Deleted);
        store.Setup(value => value.DeleteAsync(tokens[1], It.Is<CancellationToken>(token => token.CanBeCanceled)))
            .ThrowsAsync(new TimeoutException("Controlled storage failure."));
        using ServiceProvider services = CreateServices(store.Object);

        object? result = await Invoke(services, typeof(LargePayloadDeleteFunction), nameof(LargePayloadDeleteFunction.Delete),
            tokens, "purge-instance");

        List<BlobPurgeOutcome> outcomes = Assert.IsType<List<BlobPurgeOutcome>>(result);
        Assert.Equal(new[] { LargePayloadPurgeDisposition.Deleted, LargePayloadPurgeDisposition.Retry },
            outcomes.Select(outcome => outcome.Disposition));
        store.VerifyAll();
        Assert.Equal(2, store.Invocations.Count);
    }

    [Fact]
    public async Task GeneratedReportUsesBoundClientAndPreservesResultList()
    {
        var purge = new Mock<ILargePayloadPurgeClient>(MockBehavior.Strict);
        var results = new List<LargePayloadPurgeResult>
        {
            new("opaque:/+==", LargePayloadPurgeDisposition.Deleted),
            new("retry", LargePayloadPurgeDisposition.Retry),
        };
        purge.Setup(value => value.ReportLargePayloadPurgeResultsAsync(results, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        using ServiceProvider services = CreateServices();

        object? result = await Invoke(services, typeof(LargePayloadPurgeFunctions), nameof(LargePayloadPurgeFunctions.Report),
            results, "purge-instance", BoundClient(purge.Object));

        Assert.Null(result);
        Assert.Same(results, Assert.Single(purge.Invocations).Arguments[0]);
        purge.VerifyAll();
    }

    private static FunctionsDurableTaskClient BoundClient(ILargePayloadPurgeClient purge) =>
        new(new Mock<DurableTaskClient>("bound-hub").Object, null, null, purge);

    private static ServiceProvider CreateServices(PayloadStore? store = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFunctionsWorkerCore();
        if (store != null)
        {
            services.AddSingleton(store);
        }

        return services.BuildServiceProvider();
    }

    private static async Task<object?> Invoke(IServiceProvider services, Type declaringType, string method, params object[] arguments)
    {
        var definition = new Mock<FunctionDefinition>();
        definition.SetupGet(value => value.EntryPoint).Returns($"{declaringType.FullName}.{method}");
        var context = new Mock<FunctionContext>();
        context.SetupGet(value => value.FunctionDefinition).Returns(definition.Object);
        context.SetupGet(value => value.InstanceServices).Returns(services);
        var features = new InvocationFeatures();
        var input = new Mock<IFunctionInputBindingFeature>();
        input.Setup(value => value.BindFunctionInputAsync(context.Object))
            .Returns(new ValueTask<FunctionInputBindingResult>(new FunctionInputBindingResult(arguments)));
        features.Set(input.Object);

        // Worker Core keeps its result feature internal. Use its actual feature contract,
        // with normal Moq property storage, rather than replacing generated invocation code.
        Type bindingsType = typeof(FunctionContext).Assembly.GetType(
            "Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature", throwOnError: true)!;
        var bindings = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(bindingsType))!;
        bindings.GetType().GetMethod("SetupAllProperties")!.Invoke(bindings, null);
        features.Values.Add(bindingsType, bindings.Object);
        context.SetupGet(value => value.Features).Returns(features);

        IFunctionExecutor executor = GeneratedExecutor.Create(services.GetRequiredService<IFunctionActivator>());
        Assert.Equal("DirectFunctionExecutor", executor.GetType().Name);
        Assert.Equal(typeof(GeneratedExecutor).Assembly, executor.GetType().Assembly);
        await executor.ExecuteAsync(context.Object);
        input.VerifyAll();
        return context.Object.GetInvocationResult().Value;
    }

    private sealed class InvocationFeatures : IInvocationFeatures
    {
        public Dictionary<Type, object> Values { get; } = new();

        public T? Get<T>() => this.Values.TryGetValue(typeof(T), out object? value) ? (T)value : default;

        public void Set<T>(T instance) => this.Values[typeof(T)] = instance!;

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => this.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
