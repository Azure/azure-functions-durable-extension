// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker;

internal class HttpHeadersHelper
{
    internal static IDictionary<string, StringValues> CreateCopy(IDictionary<string, StringValues>? input)
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