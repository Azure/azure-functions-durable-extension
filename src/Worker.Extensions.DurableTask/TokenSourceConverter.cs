// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// JSON converter for TokenSource implementations - handles serialization only.
/// Deserialization is handled by WebJobs.Extensions.DurableTask.
/// </summary>
public class TokenSourceConverter : JsonConverter<TokenSource>
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(TokenSource).IsAssignableFrom(typeToConvert);
    }

    /// <inheritdoc/>
    public override TokenSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialization is handled by WebJobs.Extensions.DurableTask
        // We don't need to implement this for Worker.Extensions.DurableTask
        throw new NotImplementedException("Deserialization is handled by WebJobs.Extensions.DurableTask");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TokenSource value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Use the same serialization pattern as WebJobs.Extensions.DurableTask
        writer.WriteStartObject();
        
        // Currently the kind must be AzureManagedIdentity. This is a limitation of the WebJobs.Extensions.DurableTask package.
        writer.WriteString("kind", "AzureManagedIdentity");
        writer.WriteString("resource", value.Resource);

        // Handle specific token source types
        switch (value)
        {
            case ManagedIdentityTokenSource managedIdentityTokenSource:
                if (managedIdentityTokenSource.Options != null)
                {
                    writer.WritePropertyName("options");
                    JsonSerializer.Serialize(writer, managedIdentityTokenSource.Options, options);
                }
                break;
            
            default:
                throw new NotSupportedException($"Token source type '{value.GetType().Name}' is not supported for serialization. Only ManagedIdentityTokenSource is currently supported.");
        }

        writer.WriteEndObject();
    }
}
