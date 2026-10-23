// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal static class ExternalizedPayloads
{
    private static readonly HashSet<string> PayloadFields = new(StringComparer.Ordinal)
    {
        "input", "output", "result", "customStatus", "data", "reason", "details", "errorMessage", "stackTrace",
    };

    public static async Task<string> TransformInvocationAsync<T>(
        string encoded,
        MessageParser<T> parser,
        IExternalizedPayloads payloads,
        bool externalize,
        CancellationToken cancellationToken)
        where T : IMessage<T>
    {
        T message = parser.ParseFrom(Convert.FromBase64String(encoded));
        await TransformAsync(message, payloads, externalize, cancellationToken);
        return Convert.ToBase64String(message.ToByteArray());
    }

    public static async Task TransformAsync(
        IMessage message,
        IExternalizedPayloads payloads,
        bool externalize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<PayloadField> fields = new();
        Collect(message, fields, skipScheduledInputs: !externalize && message.Descriptor.Name == "OrchestratorRequest");

        // Validate the entire response before the first upload or token replacement.
        if (externalize)
        {
            foreach (PayloadField field in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();
                payloads.ValidateSize(field.Value);
            }
        }

        Dictionary<string, string?> transformed = new(StringComparer.Ordinal);
        foreach (PayloadField field in fields)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!transformed.TryGetValue(field.Value, out string? value))
            {
                value = externalize
                    ? await payloads.ExternalizeAsync(field.Value, cancellationToken)
                    : await payloads.ResolveAsync(field.Value, cancellationToken);
                transformed.Add(field.Value, value);
            }

            if (!string.Equals(value, field.Value, StringComparison.Ordinal))
            {
                field.Descriptor.Accessor.SetValue(field.Message, value);
            }
        }
    }

    private static void Collect(IMessage message, List<PayloadField> fields, bool skipScheduledInputs)
    {
        // Entity protocol envelopes need separate correlation-aware processing. Leave their
        // existing transport unchanged rather than externalizing an envelope the host must parse.
        if (message.Descriptor.Name.IndexOf("Entity", StringComparison.Ordinal) >= 0)
        {
            return;
        }

        foreach (FieldDescriptor field in message.Descriptor.Fields.InDeclarationOrder())
        {
            object? value = field.Accessor.GetValue(message);
            if (value is string text && PayloadFields.Contains(field.JsonName))
            {
                if (!skipScheduledInputs || message.Descriptor.Name != "TaskScheduledEvent" || field.JsonName != "input")
                {
                    fields.Add(new PayloadField(message, field, text));
                }
            }
            else if (value is IMessage child)
            {
                Collect(child, fields, skipScheduledInputs);
            }
            else if (field.IsRepeated && !field.IsMap && value is IEnumerable children)
            {
                foreach (object childValue in children)
                {
                    if (childValue is IMessage childMessage)
                    {
                        Collect(childMessage, fields, skipScheduledInputs);
                    }
                }
            }
        }
    }

    private sealed class PayloadField(IMessage message, FieldDescriptor descriptor, string value)
    {
        public IMessage Message { get; } = message;

        public FieldDescriptor Descriptor { get; } = descriptor;

        public string Value { get; } = value;
    }
}
