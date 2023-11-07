// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        // Method
        writer.WriteString("method", request.Method?.ToString());
        
        // URI
        writer.WriteString("uri", request.Uri?.ToString());

        // Headers
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

        // Content
        writer.WriteString("content", request.Content);

        // Asynchronous pattern enabled
        writer.WriteBoolean("asynchronousPatternEnabled", request.AsynchronousPatternEnabled);

        // Timeout
        writer.WriteString("timeout", request.Timeout.ToString());

        // HTTP retry options
        writer.WriteStartObject("retryOptions");
        writer.WriteString("FirstRetryInterval", request.HttpRetryOptions.FirstRetryInterval.ToString());
        writer.WriteString("MaxRetryInterval", request.HttpRetryOptions.MaxRetryInterval.ToString());
        writer.WriteNumber("BackoffCoefficient", request.HttpRetryOptions.BackoffCoefficient);
        writer.WriteString("RetryTimeout", request.HttpRetryOptions.RetryTimeout.ToString());
        writer.WriteNumber("MaxNumberOfAttempts", request.HttpRetryOptions.MaxNumberOfAttempts);
        writer.WriteStartArray("StatusCodesToRetry");
        foreach (HttpStatusCode statusCode in request.HttpRetryOptions.StatusCodesToRetry)
        {
            writer.WriteNumberValue((decimal)statusCode);
        }
        
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}