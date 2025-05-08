using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public sealed class TestCollectionItem
{
}

public sealed class OrchestrationInputConverterTests
{
    /// <summary>
    /// JSON serializer typically deserialize collections as List{T} instances.
    /// An instance of List{T} can be converted to any of the following interfaces or types:
    /// </summary>
    private static readonly Type[] GenericCollectionTypes =
    {
        typeof(IEnumerable<>),
        typeof(IList<>),
        typeof(ICollection<>),
        typeof(IReadOnlyList<>),
        typeof(IReadOnlyCollection<>),
        typeof(List<>)
    };

    /// <summary>
    /// System under test.
    /// </summary>
    private readonly IInputConverter inputConverter = new OrchestrationInputConverter();

    private readonly Mock<FunctionContext> functionContextMock = new();

    private readonly Mock<ConverterContext> converterContextMock = new();

    public OrchestrationInputConverterTests()
    {
        // Default setup for ConvertAsync method:
        //   - converterContext.Source should be null.
        //   - converterContext.FunctionContext should be functionContextMock.Object.
        converterContextMock.SetupGet(convCtx => convCtx.Source).Returns(default(object));
        converterContextMock.SetupGet(convCtx => convCtx.FunctionContext).Returns(functionContextMock.Object);
    }

    /// <summary>
    /// Generate test cases for each of the generic collection types.
    /// </summary>
    /// <param name="type">Collection item type.</param>
    /// <returns>Test data.</returns>
    public static TheoryData<Type> GenerateConcreteTestCollectionTypesFor(Type type)
    {
        return new TheoryData<Type>(GenericCollectionTypes.Select(ciType => ciType.MakeGenericType(type)));
    }

    [Theory(DisplayName = "ConvertAsync: Deserialized value is List<T> and target type is collection or collection interface of T")]
    [MemberData(nameof(GenerateConcreteTestCollectionTypesFor), typeof(TestCollectionItem))]
    public async Task ConvertAsync_WhenDeserializedValueIsListOfT_AndTargetTypeIsCollectionInterfaceOfT_ReturnsConversionResultSuccess(Type collectionType)
    {
        var inputData = new List<TestCollectionItem> {new(), new(), new()};
        var functionContextItems = new Dictionary<object, object> {{"__orchestrationInput__", inputData}};
        functionContextMock.SetupGet(funcCtx => funcCtx.Items).Returns(functionContextItems);
        converterContextMock.SetupGet(convCtx => convCtx.TargetType).Returns(collectionType);

        ConversionResult result = await inputConverter.ConvertAsync(converterContextMock.Object);

        // Assert conversion is successful
        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        Assert.Same(inputData, result.Value);
        // Assert that the input data has been removed from the function context items
        Assert.Empty(functionContextItems);
    }
}
