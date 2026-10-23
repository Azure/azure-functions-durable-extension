// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ExternalizedActivityExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivityOutput_IsExternalizedBeforeHostReturn(bool registeredActivity)
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider payloadServices = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = payloadServices.GetRequiredService<IExternalizedPayloads>();
        var original = new Payload(new string('x', 1_100_000));
        string serialized = JsonSerializer.Serialize(original);
        string input = (await payloads.ExternalizeAsync(serialized, CancellationToken.None))!;
        using ServiceProvider invocationServices = CreateInvocationServices(input);
        FunctionContext context = CreateContext(invocationServices, registeredActivity);
        var inner = new Mock<IFunctionExecutor>();
        inner.Setup(executor => executor.ExecuteAsync(context))
            .Callback(() => context.GetInvocationResult().Value = original)
            .Returns(ValueTask.CompletedTask);
        ITaskActivity? activity = new EchoActivity();
        var factory = new Mock<IDurableTaskFactory>();
        factory.Setup(candidate => candidate.TryCreateActivity(
            new TaskName("PayloadEcho"), invocationServices, out activity)).Returns(true);
        var executor = new DurableFunctionExecutor(
            inner.Object,
            new ExtendedSessionsCache(),
            factory.Object,
            Options.Create(new DurableTaskWorkerOptions()),
            payloads: payloads);

        await executor.ExecuteAsync(context);

        string reference = Assert.IsType<string>(context.GetInvocationResult().Value);
        Assert.True(store.IsKnownPayloadToken(reference));
        Assert.True(reference.Length < 256);
        string hostSerializedOutput = JsonSerializer.Serialize(reference);
        Assert.Equal(serialized, await payloads.ResolveAsync(hostSerializedOutput, CancellationToken.None));
        Assert.Equal(2, store.Uploads);
        inner.Verify(candidate => candidate.ExecuteAsync(context), registeredActivity ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task AttributedActivityInput_IsHydratedBeforeDeserialization()
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        var expected = new Payload(new string('x', 1_100_000));
        string reference = (await payloads.ExternalizeAsync(JsonSerializer.Serialize(expected), CancellationToken.None))!;
        var converter = new ActivityInputConverter(Options.Create(new DurableTaskWorkerOptions()), payloads);
        FunctionContext context = CreateContext(services, registeredActivity: false);
        var conversion = new Mock<ConverterContext>();
        conversion.SetupGet(value => value.Source).Returns(reference);
        conversion.SetupGet(value => value.TargetType).Returns(typeof(Payload));
        conversion.SetupGet(value => value.FunctionContext).Returns(context);

        ConversionResult result = await converter.ConvertAsync(conversion.Object);

        Assert.Equal(expected, Assert.IsType<Payload>(result.Value));
        Assert.Equal(1, store.Downloads);
    }

    [Fact]
    public async Task ActivityWithoutOptIn_ReturnsOriginalObject()
    {
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        FunctionContext context = CreateContext(services, registeredActivity: false);
        var original = new Payload(new string('x', 1_100_000));
        var inner = new Mock<IFunctionExecutor>();
        inner.Setup(executor => executor.ExecuteAsync(context))
            .Callback(() => context.GetInvocationResult().Value = original)
            .Returns(ValueTask.CompletedTask);
        var executor = new DurableFunctionExecutor(
            inner.Object, new ExtendedSessionsCache(), Mock.Of<IDurableTaskFactory>(),
            Options.Create(new DurableTaskWorkerOptions()));

        await executor.ExecuteAsync(context);

        Assert.Same(original, context.GetInvocationResult().Value);
    }

    private static FunctionContext CreateContext(IServiceProvider services, bool registeredActivity)
    {
        var trigger = new Mock<BindingMetadata>();
        trigger.SetupGet(binding => binding.Name).Returns("input");
        trigger.SetupGet(binding => binding.Type).Returns(TriggerNames.Activity);
        var definition = new Mock<FunctionDefinition>();
        definition.SetupGet(value => value.Name).Returns("PayloadEcho");
        definition.SetupGet(value => value.EntryPoint).Returns(
            registeredActivity ? DurableFunctionExecutor.ActivityEntryPoint : "PayloadFunctions.Echo");
        definition.SetupGet(value => value.InputBindings).Returns(
            ImmutableDictionary<string, BindingMetadata>.Empty.Add("input", trigger.Object));
        Type bindingsType = typeof(FunctionContext).Assembly.GetType(
            "Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature", true)!;
        var bindings = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(bindingsType))!;
        bindings.GetType().GetMethod("SetupAllProperties")!.Invoke(bindings, null);
        var features = new Mock<IInvocationFeatures>();
        features.Setup(value => value.Get<It.IsAnyType>()).Returns(
            new InvocationFunc(invocation => invocation.Method.GetGenericArguments()[0] == bindingsType
                ? bindings.Object : null));
        var context = new Mock<FunctionContext>();
        context.SetupGet(value => value.FunctionDefinition).Returns(definition.Object);
        context.SetupGet(value => value.InstanceServices).Returns(services);
        context.SetupGet(value => value.Features).Returns(features.Object);
        var bindingContext = new Mock<BindingContext>();
        bindingContext.SetupGet(value => value.BindingData).Returns(
            new Dictionary<string, object?> { ["instanceId"] = "instance" });
        context.SetupGet(value => value.BindingContext).Returns(bindingContext.Object);
        return context.Object;
    }

    private static ServiceProvider CreateInvocationServices(string input)
    {
        Assembly assembly = typeof(FunctionContext).Assembly;
        Type cacheInterface = assembly.GetType("Microsoft.Azure.Functions.Worker.IBindingCache`1", true)!
            .MakeGenericType(typeof(ConversionResult));
        Type cacheType = assembly.GetType("Microsoft.Azure.Functions.Worker.DefaultBindingCache`1", true)!
            .MakeGenericType(typeof(ConversionResult));
        object cache = Activator.CreateInstance(cacheType)!;
        Assert.Equal(true, cacheType.GetMethod("TryAdd")!.Invoke(cache, ["input", ConversionResult.Success(input)]));
        return new ServiceCollection().AddSingleton(cacheInterface, cache).BuildServiceProvider();
    }

    public sealed record Payload(string Data);

    private sealed class EchoActivity : TaskActivity<Payload, Payload>
    {
        public override Task<Payload> RunAsync(TaskActivityContext context, Payload input) => Task.FromResult(input);
    }
}
