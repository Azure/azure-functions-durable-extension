// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Azure.Core.Serialization;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// A shim to go from <see cref="ObjectSerializer" /> to <see cref="DataConverter" />.
/// </summary>
internal class ObjectConverterShim : DataConverter
{
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

        using MemoryStream stream = new(Encoding.Unicode.GetBytes(data), false);
        return this.serializer.Deserialize(stream, targetType, default);
    }

    public override string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        using MemoryStream stream = new();
        this.serializer.Serialize(stream, value, value.GetType(), default);
        using StreamReader reader = new(stream, Encoding.Unicode);
        return reader.ReadToEnd();
    }
}
