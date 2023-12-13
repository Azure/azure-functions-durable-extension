// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;

/// <summary>
/// Response received from the HTTP request made by the Durable Function.
/// </summary>
public class DurableHttpResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableHttpResponse"/> class.
    /// </summary>
    /// <param name="statusCode">HTTP Status code returned from the HTTP call.</param>
    public DurableHttpResponse(HttpStatusCode statusCode)
    {
        this.StatusCode = statusCode;
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
    [JsonConverter(typeof(HttpHeadersConverter))]
    public IDictionary<string, StringValues>? Headers { get; init; }

    /// <summary>
    /// Content returned from an HTTP request.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}