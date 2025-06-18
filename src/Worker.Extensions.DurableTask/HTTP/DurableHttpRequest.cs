// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;

/// <summary>
/// Request used to make an HTTP call through Durable Functions.
/// </summary>
public class DurableHttpRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableHttpRequest"/> class.
    /// </summary>
    public DurableHttpRequest(HttpMethod method, Uri uri)
    {
        this.Method = method;
        this.Uri = uri;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DurableHttpRequest"/> class with specified HTTP method, URI, headers, 
    /// and an option to enable asynchronous pattern handling.
    /// </summary>
    public DurableHttpRequest(HttpMethod method, Uri uri, IDictionary<string, StringValues>? headers, bool asynchronousPatternEnabled = false)
    {
        this.Method = method;
        this.Uri = uri;
        this.Headers = headers;
        this.AsynchronousPatternEnabled = asynchronousPatternEnabled;
    }

    /// <summary>
    /// HttpMethod used in the HTTP request made by the Durable Function.
    /// </summary>
    [JsonPropertyName("method")]
    [JsonConverter(typeof(HttpMethodConverter))]
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
    public IDictionary<string, StringValues>? Headers { get; set; }

    /// <summary>
    /// Content passed with the HTTP request made by the Durable Function.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Specifies whether the Durable HTTP APIs should automatically
    /// handle the asynchronous HTTP pattern.
    /// </summary>
    [JsonPropertyName("asynchronousPatternEnabled")]
    public bool AsynchronousPatternEnabled { get; set; }

    /// <summary>
    /// Defines retry policy for handling of failures in making the HTTP Request. These could be non-successful HTTP status codes
    /// in the response, a timeout in making the HTTP call, or an exception raised from the HTTP Client library.
    /// </summary>
    [JsonPropertyName("retryOptions")]
    public HttpRetryOptions? HttpRetryOptions { get; set; }

    /// <summary>
    /// The total timeout for the original HTTP request and any
    /// asynchronous polling.
    /// </summary>
    [JsonPropertyName("timeout")]
    public TimeSpan? Timeout { get; set; }
}