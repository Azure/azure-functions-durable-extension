// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask
{
    /// <summary>
    /// JSON converter for ManagedIdentityTokenSource - handles serialization only.
    /// Deserialization is handled by WebJobs.Extensions.DurableTask.
    /// </summary>
    public class TokenSourceConverter : JsonConverter<ManagedIdentityTokenSource>
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(ManagedIdentityTokenSource).IsAssignableFrom(typeToConvert);
        }

        /// <inheritdoc/>
        public override ManagedIdentityTokenSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Deserialization is handled by WebJobs.Extensions.DurableTask
            // We don't need to implement this for Worker.Extensions.DurableTask
            throw new NotImplementedException("Deserialization is handled by WebJobs.Extensions.DurableTask");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, ManagedIdentityTokenSource value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Defensive check to ensure we're only serializing ManagedIdentityTokenSource
            if (value.GetType() != typeof(ManagedIdentityTokenSource))
            {
                throw new NotSupportedException($"Token source type {value.GetType().Name} is not supported. Only ManagedIdentityTokenSource is supported for serialization.");
            }

            // Use the same serialization pattern as WebJobs.Extensions.DurableTask
            writer.WriteStartObject();
            writer.WriteString("kind", "AzureManagedIdentity");
            writer.WriteString("resource", value.Resource);

            if (value.Options != null)
            {
                writer.WritePropertyName("options");
                JsonSerializer.Serialize(writer, value.Options, options);
            }

            writer.WriteEndObject();
        }
    }
}
