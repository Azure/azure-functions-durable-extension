// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Response received from the HTTP request made by the Durable Function.
/// </summary>
[JsonConverter(typeof(DurableHttpResponseConverter))]
public class DurableHttpResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableHttpResponse"/> class.
    /// </summary>
    /// <param name="statusCode">HTTP Status code returned from the HTTP call.</param>
    /// <param name="headers">Headers returned from the HTTP call.</param>
    /// <param name="content">Content returned from the HTTP call.</param>
    public DurableHttpResponse(
        HttpStatusCode statusCode,
        IDictionary<string, StringValues>? headers = null,
        string? content = null)
    {
        this.StatusCode = statusCode;
        this.Headers = HttpHeadersHelper.CreateCopy(headers);
        this.Content = content;
    }

    /// <summary>
    /// Status code returned from an HTTP request.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Headers in the response from an HTTP request.
    /// </summary>
    [JsonPropertyName("headers")]
    public IDictionary<string, StringValues>? Headers { get; }

    /// <summary>
    /// Content returned from an HTTP request.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; }
}