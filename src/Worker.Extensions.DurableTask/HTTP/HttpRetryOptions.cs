// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;

/// <summary>
/// Defines retry policies that can be passed as parameters to various operations.
/// </summary>
public class HttpRetryOptions
{
    // Would like to make this durability provider specific, but since this is a developer
    // facing type, that is difficult.
    private static readonly TimeSpan DefaultMaxRetryinterval = TimeSpan.FromDays(6);

    /// <summary>
    /// Creates a new instance SerializableRetryOptions with the supplied first retry and max attempts.
    /// </summary>
    public HttpRetryOptions(IList<HttpStatusCode>? statusCodesToRetry = null)
    {
        this.StatusCodesToRetry = statusCodesToRetry ?? new List<HttpStatusCode>();
    }

    /// <summary>
    /// Gets or sets the first retry interval.
    /// </summary>
    /// <value>
    /// The TimeSpan to wait for the first retries.
    /// </value>
    [JsonPropertyName("FirstRetryInterval")]
    public TimeSpan FirstRetryInterval { get; set; }

    /// <summary>
    /// Gets or sets the max retry interval.
    /// </summary>
    /// <value>
    /// The TimeSpan of the max retry interval, defaults to 6 days.
    /// </value>
    [JsonPropertyName("MaxRetryInterval")]
    public TimeSpan MaxRetryInterval { get; set; } = DefaultMaxRetryinterval;

    /// <summary>
    /// Gets or sets the backoff coefficient.
    /// </summary>
    /// <value>
    /// The backoff coefficient used to determine rate of increase of backoff. Defaults to 1.
    /// </value>
    [JsonPropertyName("BackoffCoefficient")]
    public double BackoffCoefficient { get; set; } = 1;

    /// <summary>
    /// Gets or sets the timeout for retries.
    /// </summary>
    /// <value>
    /// The TimeSpan timeout for retries, defaults to <see cref="TimeSpan.MaxValue"/>.
    /// </value>
    [JsonPropertyName("RetryTimeout")]
    public TimeSpan RetryTimeout { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Gets or sets the max number of attempts.
    /// </summary>
    /// <value>
    /// The maximum number of retry attempts.
    /// </value>
    [JsonPropertyName("MaxNumberOfAttempts")]
    public int MaxNumberOfAttempts { get; set; }

    /// <summary>
    /// Gets or sets the list of status codes upon which the
    /// retry logic specified by this object shall be triggered.
    /// If none are provided, all 4xx and 5xx status codes
    /// will be retried.
    /// </summary>
    [JsonPropertyName("StatusCodesToRetry")]
    public IList<HttpStatusCode> StatusCodesToRetry { get; }
}
