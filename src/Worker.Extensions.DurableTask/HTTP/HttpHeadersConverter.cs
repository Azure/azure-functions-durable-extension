// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;

// StringValues does not deserialize as you would expect, so we need a custom mechanism
// for serializing HTTP header collections
internal class HttpHeadersConverter : JsonConverter<IDictionary<string, StringValues>>
{
    public override IDictionary<string, StringValues> Read(
        ref Utf8JsonReader reader,
        Type objectType,
        JsonSerializerOptions options)
    {
        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return headers;
        }

        var valueList = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string propertyName = reader.GetString()!;

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
                    valueList.Add(reader.GetString()!);
                }

                values = new StringValues(valueList.ToArray());
                valueList.Clear();
            }

            headers[propertyName] = values;
        }

        return headers;
    }

    public override void Write(
        Utf8JsonWriter writer,
        IDictionary<string, StringValues> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (KeyValuePair<string, StringValues> pair in value)
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

        writer.WriteEndObject();
    }
}