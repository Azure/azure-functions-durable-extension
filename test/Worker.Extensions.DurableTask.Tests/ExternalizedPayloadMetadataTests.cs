// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ExternalizedPayloadMetadataTests
{
    [Theory]
    [InlineData("activityTrigger", "Binary", "String")]
    [InlineData("ActivityTrigger", "binary", "String")]
    [InlineData("activityTrigger", "String", "String")]
    [InlineData("blobTrigger", "Binary", "Binary")]
    [InlineData("entityTrigger", "Binary", "Binary")]
    public void OptIn_RequestsSerializedTransport_OnlyForBinaryActivityInputs(
        string triggerType, string originalDataType, string expectedDataType)
    {
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(
            new ExternalizedPayloadsTests.MemoryPayloadStore());
        IFunctionMetadataTransformer transformer = Assert.Single(services.GetServices<IFunctionMetadataTransformer>());
        List<string> bindings =
        [
            $$$"""{"type":"{{{triggerType}}}","name":"input","direction":"In","dataType":"{{{originalDataType}}}","properties":{"custom":"preserved"}}""",
            """{"type":"http","name":"$return","direction":"Out"}""",
        ];
        string original = bindings[0];
        var metadata = new Mock<IFunctionMetadata>();
        metadata.SetupGet(value => value.RawBindings).Returns(bindings);

        transformer.Transform([metadata.Object]);

        using JsonDocument binding = JsonDocument.Parse(bindings[0]);
        Assert.Equal(expectedDataType, binding.RootElement.GetProperty("dataType").GetString());
        Assert.Equal("preserved", binding.RootElement.GetProperty("properties").GetProperty("custom").GetString());
        Assert.Equal("""{"type":"http","name":"$return","direction":"Out"}""", bindings[1]);
        if (originalDataType == expectedDataType)
        {
            Assert.Equal(original, bindings[0]);
        }
    }

    [Fact]
    public void WithoutOptIn_DoesNotRegisterPayloadMetadataTransformer()
    {
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();

        Assert.Empty(services.GetServices<IFunctionMetadataTransformer>());
    }
}
