// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker;

internal class DurableHttpRequestConverter : JsonConverter<DurableHttpRequest>
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(DurableHttpRequest).IsAssignableFrom(objectType);
    }

    public override DurableHttpRequest Read(
        ref Utf8JsonReader reader,
        Type objectType,
        JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        DurableHttpRequest request,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // method
        writer.WriteString("method", request.Method?.ToString());
        
        // uri
        writer.WriteString("uri", request.Uri?.ToString());

        // headers
        writer.WriteStartObject("headers");

        var headers = request.Headers;
        if (headers != null)
        {
            foreach (var pair in headers)
            {
                if (pair.Value.Count == 1)
                {
                    // serialize as a single string value
                    writer.WriteString(pair.Key, pair.Value[0]);
                }
                else
                {
                    // serializes as an array
                    writer.WriteStartArray(pair.Key);
                    writer.WriteStringValue(pair.Value);
                    writer.WriteEndArray();
                }
            }
        }

        writer.WriteEndObject();

        // content
        writer.WriteString("content", request.Content);

        // asynchronous pattern enabled
        writer.WriteBoolean("asynchronousPatternEnabled", request.AsynchronousPatternEnabled);

        writer.WriteEndObject();
    }
}
