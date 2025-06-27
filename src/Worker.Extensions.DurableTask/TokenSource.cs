// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// Abstract base class for token sources that can be inherited for future token credential types.
/// This class provides a common foundation for different token source implementations.
/// </summary>
public abstract class TokenSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenSource"/> class.
    /// </summary>
    /// <param name="resource">The resource identifier for the token source.</param>
    internal TokenSource(string resource)
    {
        this.Resource = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    /// <summary>
    /// Gets the resource identifier for the token source.
    /// </summary>
    [JsonPropertyName("resource")]
    public string Resource { get; }
} 