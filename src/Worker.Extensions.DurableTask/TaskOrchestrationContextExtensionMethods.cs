// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
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
    private const int DefaultPollingIntervalMilliseconds = 30000;
    private const string PollingInterval = "HttpDefaultAsyncRequestSleepTimeMilliseconds";

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
        ILogger logger = context.CreateReplaySafeLogger("Microsoft.Azure.Functions.Worker.Extensions.DurableTask.CallHttp");

#pragma warning disable DURABLE2003 // BuiltIn::HttpActivity is a reserved framework activity, not user-defined
        DurableHttpResponse response = await context.CallActivityAsync<DurableHttpResponse>(Constants.HttpTaskActivityReservedName, request);
#pragma warning restore DURABLE2003
        
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

            if (headersDictionary.TryGetValue("Retry-After", out StringValues retryAfterStr) && int.TryParse(retryAfterStr, out int retryAfter))
            {
                fireAt = context.CurrentUtcDateTime.AddSeconds(retryAfter);
            }
            else
            {
                // Gets configuration DefaultAsyncRequestSleepTimeMilliseconds from DurableTaskExtension.
                // If no value is provided, then use the default 30000 milliseconds.
                int asyncRequestSleepTimeMilliseconds = context.Properties.TryGetValue(PollingInterval, out var value) && value is double d
                                                                ? (int)d: DefaultPollingIntervalMilliseconds;
                fireAt = context.CurrentUtcDateTime.AddMilliseconds(asyncRequestSleepTimeMilliseconds);
            }

            await context.CreateTimer(fireAt, CancellationToken.None);

            string? locationUrl = response.Headers["Location"];

            if (locationUrl is null)
            {
                logger.LogWarning("HTTP response received but 'Location' header is missing; unable to poll for status.");
                break;
            }

            DurableHttpRequest newHttpRequest = CreateLocationPollRequest(request, locationUrl);

            logger.LogInformation($"Polling HTTP status at location: {locationUrl}");

#pragma warning disable DURABLE2003 // BuiltIn::HttpActivity is a reserved framework activity, not user-defined
            response = await context.CallActivityAsync<DurableHttpResponse>(Constants.HttpTaskActivityReservedName, newHttpRequest);
#pragma warning restore DURABLE2003
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
    public static Task<DurableHttpResponse> CallHttpAsync(
        this TaskOrchestrationContext context,
        HttpMethod method,
        Uri uri,
        string? content = null,
        HttpRetryOptions? retryOptions = null)
    {
        return CallHttpAsync(context, method, uri, content, retryOptions, false);
    }

    /// <summary>
    /// Makes an HTTP call to the specified uri.
    /// </summary>
    /// <param name="context">The task orchestration context.</param>
    /// <param name="method">HttpMethod used for api call.</param>
    /// <param name="uri">uri used to make the HTTP call.</param>
    /// <param name="content">Content passed in the HTTP request.</param>
    /// <param name="retryOptions">The retry option for the HTTP task.</param>
    /// <param name="asynchronousPatternEnabled">Boolean controls Whether Durable HTTP should automatically handle async HTTP patterns like 202 with polling. Default to false. </param>
    /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
    public static Task<DurableHttpResponse> CallHttpAsync(
        this TaskOrchestrationContext context, 
        HttpMethod method,
        Uri uri,
        string? content = null,
        HttpRetryOptions? retryOptions = null,
        bool asynchronousPatternEnabled = false)
    {
        DurableHttpRequest request = new DurableHttpRequest(method, uri)
        {
            Content = content,
            HttpRetryOptions = retryOptions,
            AsynchronousPatternEnabled = asynchronousPatternEnabled,
        };

        return context.CallHttpAsync(request);
    }

    /// <summary>
    /// Makes an HTTP call to the specified uri with token source for authentication.
    /// </summary>
    /// <param name="context">The task orchestration context.</param>
    /// <param name="method">HttpMethod used for api call.</param>
    /// <param name="uri">uri used to make the HTTP call.</param>
    /// <param name="content">Content passed in the HTTP request.</param>
    /// <param name="retryOptions">The retry option for the HTTP task.</param>
    /// <param name="asynchronousPatternEnabled">Boolean controls Whether Durable HTTP should automatically handle async HTTP patterns like 202 with polling. Default to false.</param>
    /// <param name="tokenSource">Token source for authentication.</param>
    /// <param name="timeout">TimeSpan used for HTTP request timeout.</param>
    /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
    public static Task<DurableHttpResponse> CallHttpAsync(
        this TaskOrchestrationContext context,
        HttpMethod method,
        Uri uri,
        string? content = null,
        HttpRetryOptions? retryOptions = null,
        bool asynchronousPatternEnabled = false,
        TokenSource? tokenSource = null,
        TimeSpan? timeout = null)
    {
        DurableHttpRequest request = new DurableHttpRequest(method, uri)
        { 
            Content = content,
            HttpRetryOptions = retryOptions,
            AsynchronousPatternEnabled = asynchronousPatternEnabled,
            TokenSource = tokenSource,
            Timeout = timeout
        };

        return context.CallHttpAsync(request);
    }

    /// <summary>
    /// Gets the <see cref="FunctionContext"/> associated with the current orchestration context.
    /// </summary>
    /// <remarks>
    /// This method is intended for use in Azure Functions environments where additional function-level
    /// context is needed. If the <paramref name="context"/> is not backed by an Azure Functions
    /// orchestration, the method returns <c>null</c>.
    /// </remarks>
    /// <param name="context">The <see cref="TaskOrchestrationContext"/> from which to obtain the <see cref="FunctionContext"/>.</param>
    /// <returns>The <see cref="FunctionContext"/> if available; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="context"/> is <c>null</c>.</exception>
    public static FunctionContext? GetFunctionContext(this TaskOrchestrationContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context is FunctionsOrchestrationContext functionsContext)
        {
            return functionsContext.FunctionContext;
        }

        return null;
    }

    internal static DurableHttpRequest CreateLocationPollRequest(DurableHttpRequest durableHttpRequest, string locationUri)
    {
        // Resolve relative Location URIs against the original request URI. A relative
        // redirect is inherently same-origin. Using the two-argument Uri constructor
        // also avoids a UriFormatException that new Uri(string) would throw for
        // non-absolute URIs.
        Uri parsedLocationUri = durableHttpRequest.Uri is not null && durableHttpRequest.Uri.IsAbsoluteUri
            ? new Uri(durableHttpRequest.Uri, locationUri)
            : new Uri(locationUri);

        // When following a 202 Location redirect to a different origin, do not forward
        // credentials (Authorization/Cookie headers). This matches the same-origin policy
        // applied by the Fetch Standard (HTTP-redirect fetch, step 13) and is more
        // permissive than .NET's HttpClient, which clears Authorization on every redirect
        // (see SocketsHttpHandler's RedirectHandler in dotnet/runtime). Same-origin
        // forwarding is intentional here because the async HTTP polling pattern
        // legitimately needs the caller's headers to follow the Location header back
        // to the same service. The check prevents an attacker-controlled first-hop
        // server from harvesting credentials by redirecting the poll to a host they
        // control.
        bool sameOrigin = IsSameOrigin(durableHttpRequest.Uri!, parsedLocationUri);

        // Make a defensive copy of the headers dictionary so the mutations below do not
        // leak back to the original request (the poll loop reuses `request` across
        // iterations as the basis for each new poll).
        IDictionary<string, StringValues>? headersCopy = durableHttpRequest.Headers is null
            ? null
            : new Dictionary<string, StringValues>(durableHttpRequest.Headers, StringComparer.OrdinalIgnoreCase);

        if (headersCopy is not null)
        {
            // Do not copy over the x-functions-key header, as in many cases, the
            // functions key used for the initial request will be a Function-level key
            // and the status endpoint requires a master key.
            headersCopy.Remove("x-functions-key");

            if (!sameOrigin)
            {
                // Strip Authorization and Cookie headers when redirecting cross-origin so
                // credentials a caller set directly on the request are not leaked.
                headersCopy.Remove("Authorization");
                headersCopy.Remove("Cookie");
            }
        }

        DurableHttpRequest newDurableHttpRequest = new DurableHttpRequest(
            method: HttpMethod.Get,
            uri: parsedLocationUri,
            headers: headersCopy,
            asynchronousPatternEnabled: durableHttpRequest.AsynchronousPatternEnabled);

        return newDurableHttpRequest;
    }

    private static bool IsSameOrigin(Uri original, Uri redirect)
    {
        if (original is null || redirect is null)
        {
            return false;
        }

        if (!original.IsAbsoluteUri || !redirect.IsAbsoluteUri)
        {
            // Treat any non-absolute URI as cross-origin to err on the side of stripping credentials.
            return false;
        }

        return string.Equals(original.Scheme, redirect.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(original.Host, redirect.Host, StringComparison.OrdinalIgnoreCase)
            && original.Port == redirect.Port;
    }

}
