// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask
{
    /// <summary>
    /// JSON converter for ITokenSource implementations - handles serialization only.
    /// Deserialization is handled by WebJobs.Extensions.DurableTask.
    /// </summary>
    public class TokenSourceConverter : JsonConverter<ITokenSource>
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(ITokenSource).IsAssignableFrom(typeToConvert);
        }

        /// <inheritdoc/>
        public override ITokenSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Deserialization is handled by WebJobs.Extensions.DurableTask
            // We don't need to implement this for Worker.Extensions.DurableTask
            throw new NotImplementedException("Deserialization is handled by WebJobs.Extensions.DurableTask");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, ITokenSource value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (value is ManagedIdentityTokenSource tokenSource)
            {
                // Use the same serialization pattern as WebJobs.Extensions.DurableTask
                writer.WriteStartObject();
                writer.WriteString("kind", "AzureManagedIdentity");
                writer.WriteString("resource", tokenSource.Resource);

                if (tokenSource.Options != null)
                {
                    writer.WritePropertyName("options");
                    JsonSerializer.Serialize(writer, tokenSource.Options, options);
                }

                writer.WriteEndObject();
            }
            else
            {
                // Only ManagedIdentityTokenSource is supported for serialization
                // Other ITokenSource implementations should use the "kind" pattern
                throw new NotSupportedException($"Token source type {value.GetType().Name} is not supported. Only ManagedIdentityTokenSource is supported for serialization.");
            }
        }
    }
} 