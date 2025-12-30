// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ObjectConverterShimTests
{
    [Fact]
    public void Serialize_UsesDeclaredTypeHintWhenProvided()
    {
        JsonSerializerOptions options = CreateOptions();
        JsonObjectSerializer serializer = new(options);
        ObjectConverterShim converter = new(serializer);

        DerivedResponse value = new() { Field1 = 42, Field2 = 99 };

        string runtimeJson = JsonSerializer.Serialize(value, options);
        string declaredJson = JsonSerializer.Serialize<BaseResponse>(value, options);

        Assert.NotEqual(declaredJson, runtimeJson);
        Assert.Equal(runtimeJson, converter.Serialize(value));

        string hintedJson = converter.Serialize(
            ObjectConverterShim.WithDeclaredType(value, typeof(BaseResponse)))!;

        Assert.Equal(declaredJson, hintedJson);
        Assert.Equal(runtimeJson, converter.Serialize(value));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type == typeof(BaseResponse))
            {
                info.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "type",
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                };
                info.PolymorphismOptions.DerivedTypes.Add(
                    new JsonDerivedType(typeof(DerivedResponse), "derived"));
            }
        });

        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
        };
    }

    private class BaseResponse
    {
        public int Field1 { get; set; }
    }

    private class DerivedResponse : BaseResponse
    {
        public int Field2 { get; set; }
    }
}
