// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    // StringValues does not deserialize as you would expect, so we need a custom mechanism
    // for serializing HTTP header collections
    internal class HttpHeadersConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IDictionary<string, StringValues>).IsAssignableFrom(objectType);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

            if (reader.TokenType != JsonToken.StartObject)
            {
                return headers;
            }

            var valueList = new List<string>();
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                string propertyName = (string)reader.Value;

                reader.Read();

                // Header values can be either individual strings or string arrays
                StringValues values = default(StringValues);
                if (reader.TokenType == JsonToken.String)
                {
                    values = new StringValues((string)reader.Value);
                }
                else if (reader.TokenType == JsonToken.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        valueList.Add((string)reader.Value);
                    }

                    values = new StringValues(valueList.ToArray());
                    valueList.Clear();
                }

                headers[propertyName] = values;
            }

            return headers;
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            writer.WriteStartObject();

            var headers = (IDictionary<string, StringValues>)value;
            foreach (var pair in headers)
            {
                writer.WritePropertyName(pair.Key);

                if (pair.Value.Count == 1)
                {
                    // serialize as a single string value
                    writer.WriteValue(pair.Value[0]);
                }
                else
                {
                    // serializes as an array
                    serializer.Serialize(writer, pair.Value);
                }
            }

            writer.WriteEndObject();
        }

        internal static IDictionary<string, StringValues> CreateCopy(IDictionary<string, StringValues> input)
        {
            var copy = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            if (input != null)
            {
                foreach (var pair in input)
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return copy;
        }
    }
}
