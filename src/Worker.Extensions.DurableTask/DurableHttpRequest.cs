// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Request used to make an HTTP call through Durable Functions.
/// </summary>
public class DurableHttpRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableHttpRequest"/> class.
    /// </summary>
    public DurableHttpRequest(
        HttpMethod method,
        Uri uri,
        IDictionary<string, StringValues>? headers = null,
        string? content = null)
    {
        this.Method = method;
        this.Uri = uri;
        this.Headers = HttpHeadersConverter.CreateCopy(headers);
        this.Content = content;
    }

    /// <summary>
    /// HttpMethod used in the HTTP request made by the Durable Function.
    /// </summary>
    [JsonPropertyName("method")]
    public HttpMethod Method { get; }

    /// <summary>
    /// Uri used in the HTTP request made by the Durable Function.
    /// </summary>
    [JsonPropertyName("uri")]
    public Uri Uri { get; }

    /// <summary>
    /// Headers passed with the HTTP request made by the Durable Function.
    /// </summary>
    [JsonPropertyName("headers")]
    [JsonConverter(typeof(HttpHeadersConverter))]
    public IDictionary<string, StringValues>? Headers { get; }

    /// <summary>
    /// Content passed with the HTTP request made by the Durable Function.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; }

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