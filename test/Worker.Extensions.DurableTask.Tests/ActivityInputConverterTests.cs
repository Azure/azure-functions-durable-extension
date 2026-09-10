using System.Text.Json;
using Google.Protobuf;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Exceptions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Options;
using Moq;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ActivityInputConverterTests
{
    [Fact]
    public void ConvertAsync_DeserializationFails_ThrowsActionableFailure()
    {
        var originalException = new JsonException("Original deserialization error.");
        var options = new DurableTaskWorkerOptions
        {
            DataConverter = new ThrowingDataConverter(originalException),
        };
        var converter = new ActivityInputConverter(Options.Create(options));
        var functionDefinition = new Mock<FunctionDefinition>();
        functionDefinition.SetupGet(definition => definition.Name).Returns("TestActivity");
        var functionContext = new Mock<FunctionContext>();
        functionContext.SetupGet(context => context.FunctionDefinition).Returns(functionDefinition.Object);
        var converterContext = new Mock<ConverterContext>();
        converterContext.SetupGet(context => context.Source).Returns("{}");
        converterContext.SetupGet(context => context.TargetType).Returns(typeof(ITestService));
        converterContext.SetupGet(context => context.FunctionContext).Returns(functionContext.Object);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => converter.ConvertAsync(converterContext.Object));
        Assert.Contains("Failed to deserialize input for activity function 'TestActivity'", exception.Message);
        Assert.Contains($"into type '{typeof(ITestService).FullName}'", exception.Message);
        Assert.Contains("Activity inputs must be JSON-serializable values", exception.Message);
        Assert.Contains(
            "data transfer objects instead of interfaces or dependency-injected services",
            exception.Message);
        Assert.Contains("The original error was: Original deserialization error.", exception.Message);
        Assert.Same(originalException, exception.InnerException);

        var serializationException = new DurableSerializationException(exception);
        P.TaskFailureDetails failure = JsonParser.Default.Parse<P.TaskFailureDetails>(serializationException.Message);
        Assert.Equal(typeof(InvalidOperationException).FullName, failure.ErrorType);
        Assert.Equal(typeof(JsonException).FullName, failure.InnerFailure.ErrorType);
        Assert.Equal(originalException.Message, failure.InnerFailure.ErrorMessage);
    }

    private interface ITestService
    {
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
