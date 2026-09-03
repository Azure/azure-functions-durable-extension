using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class DurableFunctionExecutorActivityTests
{
    [Fact]
    public async Task ExecuteAsync_ClassBasedDeserializationFails_ThrowsActionableFailure()
    {
        var originalException = new JsonException("Original deserialization error.");
        IServiceProvider services = CreateServicesWithCachedInput("{}");
        FunctionContext context = CreateActivityContext(services);
        ITaskActivity? activity = new DeserializationFailureActivity();
        var factory = new Mock<IDurableTaskFactory>();
        factory
            .Setup(candidate => candidate.TryCreateActivity(
                new TaskName("TestActivity"),
                services,
                out activity))
            .Returns(true);
        var options = Options.Create(new DurableTaskWorkerOptions
        {
            DataConverter = new ThrowingDataConverter(originalException),
        });
        var executor = new DurableFunctionExecutor(
            Mock.Of<IFunctionExecutor>(),
            new ExtendedSessionsCache(),
            factory.Object,
            options);

        DurableSerializationException exception = await Assert.ThrowsAsync<DurableSerializationException>(
            () => executor.ExecuteAsync(context).AsTask());

        P.TaskFailureDetails failure = JsonParser.Default.Parse<P.TaskFailureDetails>(exception.Message);
        Assert.Equal(typeof(InvalidOperationException).FullName, failure.ErrorType);
        Assert.Contains("Failed to deserialize input for activity function 'TestActivity'", failure.ErrorMessage);
        Assert.Contains($"into type '{typeof(ITestService).FullName}'", failure.ErrorMessage);
        Assert.Contains("Activity inputs must be JSON-serializable values", failure.ErrorMessage);
        Assert.Contains(
            "data transfer objects instead of interfaces or dependency-injected services",
            failure.ErrorMessage);
        Assert.Contains("The original error was: Original deserialization error.", failure.ErrorMessage);
        Assert.Equal(typeof(JsonException).FullName, failure.InnerFailure.ErrorType);
        Assert.Equal(originalException.Message, failure.InnerFailure.ErrorMessage);
        Assert.Same(originalException, exception.InnerException);
    }

    private static FunctionContext CreateActivityContext(IServiceProvider services)
    {
        var triggerBinding = new Mock<BindingMetadata>();
        triggerBinding.SetupGet(binding => binding.Name).Returns("input");
        triggerBinding.SetupGet(binding => binding.Type).Returns(TriggerNames.Activity);

        var functionDefinition = new Mock<FunctionDefinition>();
        functionDefinition.SetupGet(definition => definition.Name).Returns("TestActivity");
        functionDefinition.SetupGet(definition => definition.EntryPoint)
            .Returns(DurableFunctionExecutor.ActivityEntryPoint);
        functionDefinition.SetupGet(definition => definition.InputBindings)
            .Returns(ImmutableDictionary<string, BindingMetadata>.Empty.Add("input", triggerBinding.Object));

        var context = new Mock<FunctionContext>();
        context.SetupGet(candidate => candidate.FunctionDefinition).Returns(functionDefinition.Object);
        context.SetupGet(candidate => candidate.InstanceServices).Returns(services);
        return context.Object;
    }

    private static IServiceProvider CreateServicesWithCachedInput(string input)
    {
        Assembly workerAssembly = typeof(FunctionContext).Assembly;
        Type cacheInterface = workerAssembly.GetType("Microsoft.Azure.Functions.Worker.IBindingCache`1", true)!
            .MakeGenericType(typeof(ConversionResult));
        Type cacheImplementation = workerAssembly.GetType("Microsoft.Azure.Functions.Worker.DefaultBindingCache`1", true)!
            .MakeGenericType(typeof(ConversionResult));
        object cache = Activator.CreateInstance(cacheImplementation)!;
        MethodInfo tryAdd = cacheImplementation.GetMethod("TryAdd")!;
        Assert.Equal(true, tryAdd.Invoke(cache, ["input", ConversionResult.Success(input)]));

        return new ServiceCollection()
            .AddSingleton(cacheInterface, cache)
            .BuildServiceProvider();
    }

    private interface ITestService
    {
    }

    private sealed class DeserializationFailureActivity : TaskActivity<ITestService, object?>
    {
        public override Task<object?> RunAsync(TaskActivityContext context, ITestService input)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingDataConverter(Exception exception) : DataConverter
    {
        public override object? Deserialize(string? data, Type targetType)
        {
            throw exception;
        }

        public override string? Serialize(object? value)
        {
            throw new NotSupportedException();
        }
    }
}
