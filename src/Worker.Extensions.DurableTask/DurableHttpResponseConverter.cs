// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker;
internal class DurableHttpResponseConverter : JsonConverter<DurableHttpResponse>
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(DurableHttpResponse).IsAssignableFrom(objectType);
    }

    public override DurableHttpResponse Read(
        ref Utf8JsonReader reader,
        Type objectType,
        JsonSerializerOptions options)
    {
        DurableHttpResponse response;
        HttpStatusCode statusCode = HttpStatusCode.Moved;
        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        string content = "";

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            // Get the key.
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            string? propertyName = reader.GetString();

            // status code
            if (string.Equals(propertyName, "statusCode"))
            {
                reader.Read();
                statusCode = (HttpStatusCode)reader.GetInt64();
                continue;
            }

            // content
            if (string.Equals(propertyName, "content"))
            {
                reader.Read();
                content = reader.GetString();
                continue;
            }

            // headers
            if (string.Equals(propertyName, "headers"))
            {
                reader.Read();

                var valueList = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    string? headerName = reader.GetString();

                    reader.Read();

                    // Header values can be either individual strings or string arrays
                    StringValues values = default(StringValues);
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        values = new StringValues(reader.GetString());
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            valueList.Add(reader.GetString());
                        }

                        values = new StringValues(valueList.ToArray());
                        valueList.Clear();
                    }

                    headers[headerName] = values;
                }
            }
        }

        return new DurableHttpResponse(statusCode, headers, content);
    }

    public override void Write(
        Utf8JsonWriter writer,
        DurableHttpResponse response,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // status code
        writer.WriteString("status code", response.StatusCode.ToString());

        // content
        writer.WriteString("content", response.Content);

        // headers
        writer.WriteStartObject();

        var headers = response.Headers;
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

        writer.WriteEndObject();
    }
}
