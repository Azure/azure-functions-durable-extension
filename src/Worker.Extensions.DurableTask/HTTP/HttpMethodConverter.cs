// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;

internal class HttpMethodConverter : JsonConverter<HttpMethod>
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(HttpMethod).IsAssignableFrom(objectType);
    }

    public override HttpMethod Read(
        ref Utf8JsonReader reader,
        Type objectType,
        JsonSerializerOptions options)
    {
        string readerString = reader.GetString() ?? string.Empty;
        return new HttpMethod(readerString);
    }

    public override void Write(
        Utf8JsonWriter writer,
        HttpMethod value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
