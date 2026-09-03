using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ActivityInputConverterTests
{
    [Fact]
    public async Task ConvertAsync_DeserializationFails_ReturnsActionableFailure()
    {
        var originalException = new InvalidOperationException("Original deserialization error.");
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

        ConversionResult result = await converter.ConvertAsync(converterContext.Object);

        Assert.Equal(ConversionStatus.Failed, result.Status);
        InvalidOperationException exception = Assert.IsType<InvalidOperationException>(result.Error);
        Assert.Contains("Failed to deserialize input for activity function 'TestActivity'", exception.Message);
        Assert.Contains($"into type '{typeof(ITestService).FullName}'", exception.Message);
        Assert.Contains("Activity inputs must be JSON-serializable values", exception.Message);
        Assert.Contains(
            "data transfer objects instead of interfaces or dependency-injected services",
            exception.Message);
        Assert.Contains("The original error was: Original deserialization error.", exception.Message);
        Assert.Same(originalException, exception.InnerException);
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
