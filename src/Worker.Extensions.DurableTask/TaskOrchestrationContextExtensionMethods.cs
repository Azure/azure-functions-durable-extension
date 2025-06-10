// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Microsoft.DurableTask;

/// <summary>
/// Extensions for <see cref="TaskOrchestrationContext"/>.
/// </summary>
public static class TaskOrchestrationContextExtensionMethods
{
    const int DefaultAsyncRequestSleepTimeMilliseconds = 30000;

    /// <summary>
    /// Makes an HTTP call using the information in the DurableHttpRequest.
    /// </summary>
    /// <param name="context">The task orchestration context.</param>
    /// <param name="request">The DurableHttpRequest used to make the HTTP call.</param>
    /// <returns>DurableHttpResponse</returns>
    public static async Task<DurableHttpResponse> CallHttpAsync(this TaskOrchestrationContext context, DurableHttpRequest request)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        ILogger logger = context.CreateReplaySafeLogger("CallHttpASync");

        DurableHttpResponse response = await context.CallActivityAsync<DurableHttpResponse>(Constants.HttpTaskActivityReservedName, request);
        
        while (response.StatusCode == HttpStatusCode.Accepted && request.AsynchronousPatternEnabled )
        {
            // If Headers is null or missing, we can't poll the Location URL, so return the response.
            if (response.Headers is null)
            {
                logger.LogWarning("HTTP response headers are null or missing; unable to retrieve 'Location' URL for polling.");
                break;
            }

            var headersDictionary = new Dictionary<string, StringValues>(
                       response.Headers!,
                       StringComparer.OrdinalIgnoreCase);

            DateTime fireAt = default(DateTime);

            if (headersDictionary.TryGetValue("Retry-After", out StringValues retryAfter))
            {
                fireAt = context.CurrentUtcDateTime.AddSeconds(int.Parse(retryAfter));
            }
            else
            {
                // Gets configuration DefaultAsyncRequestSleepTimeMilliseconds from DurableTaskExtension
                int asyncRequestSleepTimeMilliseconds = context.Properties.TryGetValue("df.http.defaultAsyncRequestSleepTimeMilliseconds", out var value) && value is double d
                                                                ? (int)d: DefaultAsyncRequestSleepTimeMilliseconds;
                fireAt = context.CurrentUtcDateTime.AddMilliseconds(asyncRequestSleepTimeMilliseconds);
            }

            await context.CreateTimer(fireAt, CancellationToken.None);

            string locationUrl = response.Headers!["Location"];

            DurableHttpRequest newHttpRequest = CreateLocationPollRequest(request, locationUrl);

            logger.LogInformation($"Polling HTTP status at location: {locationUrl}");

            response = await context.CallActivityAsync<DurableHttpResponse>(Constants.HttpTaskActivityReservedName, newHttpRequest);
        }

        return response;
    }

    /// <summary>
    /// Makes an HTTP call to the specified uri.
    /// </summary>
    /// <param name="context">The task orchestration context.</param>
    /// <param name="method">HttpMethod used for api call.</param>
    /// <param name="uri">uri used to make the HTTP call.</param>
    /// <param name="content">Content passed in the HTTP request.</param>
    /// <param name="retryOptions">The retry option for the HTTP task.</param>
    /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
    public static Task<DurableHttpResponse> CallHttpAsync(this TaskOrchestrationContext context, HttpMethod method, Uri uri, string? content = null, HttpRetryOptions? retryOptions = null)
    {
        DurableHttpRequest request = new DurableHttpRequest(method, uri)
        {
            Content = content,
            HttpRetryOptions = retryOptions,
            AsynchronousPatternEnabled = true,
        };

        return context.CallHttpAsync(request);
    }

    private static DurableHttpRequest CreateLocationPollRequest(DurableHttpRequest durableHttpRequest, string locationUri)
    {
        DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
            method: HttpMethod.Get,
            uri: new Uri(locationUri),
            headers: durableHttpRequest.Headers);

        return newDurableHttpRequest;
    }

}
