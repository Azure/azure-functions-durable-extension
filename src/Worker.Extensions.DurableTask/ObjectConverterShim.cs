// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using Azure.Core.Serialization;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// A shim to go from <see cref="ObjectSerializer" /> to <see cref="DataConverter" />.
/// </summary>
internal class ObjectConverterShim : DataConverter
{
    private static readonly AsyncLocal<Type?> SerializationType = new();

    private readonly ObjectSerializer serializer;

    public ObjectConverterShim(ObjectSerializer serializer)
    {
        this.serializer = serializer;
    }

    public override object? Deserialize(string? data, Type targetType)
    {
        if (data is null)
        {
            return null;
        }

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(data), false);
        return this.serializer.Deserialize(stream, targetType, default);
    }

    public override string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        Type? declaredType = SerializationType.Value ?? value.GetType();
        try
        {
            BinaryData data = this.serializer.Serialize(value, declaredType, default);
            return data.ToString();
        }
        finally
        {
            SerializationType.Value = null;
        }
    }

    internal static void SetSerializationType(Type? type)
    {
        SerializationType.Value = type;
    }
}
