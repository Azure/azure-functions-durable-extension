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
    private IDictionary<string, string[]>? crossOriginPollingHeaders;

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
    /// Token source for authentication.
    /// </summary>
    [JsonPropertyName("tokenSource")]
    [JsonConverter(typeof(TokenSourceConverter))]
    public TokenSource? TokenSource { get; set; }

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

    /// <summary>
    /// Gets or sets selected polling header names by exact HTTPS origin.
    /// </summary>
    /// <remarks>
    /// Used only when EnableCrossOriginHeaderFiltering is enabled.
    /// Assign the complete configuration. Both accessors copy the dictionary and its lists.
    /// </remarks>
    /// <exception cref="ArgumentException">The configuration contains an invalid origin or header list.</exception>
    [JsonPropertyName("crossOriginPollingHeaders")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? CrossOriginPollingHeaders
    {
        get => Microsoft.Azure.DurableTask.PollingHeaderPolicy.Copy(this.crossOriginPollingHeaders);
        set => this.crossOriginPollingHeaders = Microsoft.Azure.DurableTask.PollingHeaderPolicy.Copy(value);
    }

    /// <summary>
    /// Gets or sets whether to restrict cross-origin polling headers. Defaults to false.
    /// </summary>
    /// <remarks>
    /// When enabled, uses CrossOriginPollingHeaders for cross-origin polls and disables
    /// automatic polling redirects. When disabled, preserves existing polling behavior.
    /// </remarks>
    /// <example>
    /// <code>
    /// request.EnableCrossOriginHeaderFiltering = true;
    /// request.CrossOriginPollingHeaders = new Dictionary&lt;string, string[]&gt;
    /// {
    ///     ["https://status.example:443"] = new[] { "X-Request-Id" },
    /// };
    /// </code>
    /// </example>
    [JsonPropertyName("enableCrossOriginHeaderFiltering")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EnableCrossOriginHeaderFiltering { get; set; }

    [JsonInclude]
    [JsonPropertyName("disablePollingRedirects")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    internal bool DisablePollingRedirects { get; set; }
}