// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Extensions for <see cref="DurableTaskClient"/>
/// </summary>
public static class DurableTaskClientExtensions
{
    /// <summary>
    /// Creates an HTTP response that is useful for checking the status of the specified instance.
    /// </summary>
    /// <param name="client">The <see cref="DurableTaskClient"/>.</param>
    /// <param name="request">The HTTP request that this response is for.</param>
    /// <param name="instanceId">The ID of the orchestration instance to check.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>An HTTP 202 response with a Location header and a payload containing instance control URLs.</returns>
    public static Task<HttpResponseData> CreateCheckStatusResponseAsync(
        this DurableTaskClient client,
        HttpRequestData request,
        string instanceId,
        CancellationToken cancellation = default)
    {
        return client.CreateCheckStatusResponseAsync(request, instanceId, HttpStatusCode.Accepted, cancellation);
    }

    /// <summary>
    /// Creates an HTTP response that is useful for checking the status of the specified instance.
    /// </summary>
    /// <param name="client">The <see cref="DurableTaskClient"/>.</param>
    /// <param name="request">The HTTP request that this response is for.</param>
    /// <param name="instanceId">The ID of the orchestration instance to check.</param>
    /// <param name="statusCode">The status code.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>An HTTP response with a Location header and a payload containing instance control URLs.</returns>
    public static async Task<HttpResponseData> CreateCheckStatusResponseAsync(
        this DurableTaskClient client,
        HttpRequestData request,
        string instanceId,
        HttpStatusCode statusCode,
        CancellationToken cancellation = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        HttpResponseData response = request.CreateResponse(statusCode);
        object payload = SetHeadersAndGetPayload(client, request, response, instanceId);

        ObjectSerializer serializer = GetObjectSerializer(response);
        await serializer.SerializeAsync(response.Body, payload, payload.GetType(), cancellation);
        return response;
    }

    /// <summary>
    /// Creates an HTTP response that is useful for checking the status of the specified instance.
    /// </summary>
    /// <param name="client">The <see cref="DurableTaskClient"/>.</param>
    /// <param name="request">The HTTP request that this response is for.</param>
    /// <param name="instanceId">The ID of the orchestration instance to check.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>An HTTP 202 response with a Location header and a payload containing instance control URLs.</returns>
    public static HttpResponseData CreateCheckStatusResponse(
        this DurableTaskClient client,
        HttpRequestData request,
        string instanceId,
        CancellationToken cancellation = default)
    {
        return client.CreateCheckStatusResponse(request, instanceId, HttpStatusCode.Accepted, cancellation);
    }

    /// <summary>
    /// Creates an HTTP response that is useful for checking the status of the specified instance.
    /// </summary>
    /// <param name="client">The <see cref="DurableTaskClient"/>.</param>
    /// <param name="request">The HTTP request that this response is for.</param>
    /// <param name="instanceId">The ID of the orchestration instance to check.</param>
    /// <param name="statusCode">The status code.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>An HTTP response with a Location header and a payload containing instance control URLs.</returns>
    public static HttpResponseData CreateCheckStatusResponse(
        this DurableTaskClient client,
        HttpRequestData request,
        string instanceId,
        HttpStatusCode statusCode,
        CancellationToken cancellation = default)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        HttpResponseData response = request.CreateResponse(statusCode);
        object payload = SetHeadersAndGetPayload(client, request, response, instanceId);

        ObjectSerializer serializer = GetObjectSerializer(response);
        serializer.Serialize(response.Body, payload, payload.GetType(), cancellation);
        return response;
    }

    /// <summary>
    /// Creates an HTTP management payload for the specified orchestration instance.
    /// </summary>
    /// <param name="client">The <see cref="DurableTaskClient"/>.</param>
    /// <param name="instanceId">The ID of the orchestration instance.</param>
    /// <param name="request">Optional HTTP request data to use for creating the base URL.</param>
    /// <returns>An object containing instance control URLs.</returns>
    /// <exception cref="ArgumentException">Thrown when instanceId is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a valid base URL cannot be determined.</exception>
    public static HttpManagementPayload CreateHttpManagementPayload(
        this DurableTaskClient client,
        string instanceId,
        HttpRequestData? request = null)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            throw new ArgumentException("InstanceId cannot be null or empty.", nameof(instanceId));
        }

        return SetHeadersAndGetPayload(client, request, null, instanceId);
    }

    private static HttpManagementPayload SetHeadersAndGetPayload(
        DurableTaskClient client, HttpRequestData? request, HttpResponseData? response, string instanceId)
    {
        static string BuildUrl(string url, params string?[] queryValues)
        {
            bool appended = false;
            foreach (string? query in queryValues)
            {
                if (!string.IsNullOrEmpty(query))
                {
                    url = url + (appended ? "&" : "?") + query;
                    appended = true;
                }
            }

            return url;
        }

        // TODO: To better support scenarios involving proxies or application gateways, this
        //       code should take the X-Forwarded-Host, X-Forwarded-Proto, and Forwarded HTTP
        //       request headers into consideration and generate the base URL accordingly.
        //       More info: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Forwarded.
        //       One potential workaround is to set ASPNETCORE_FORWARDEDHEADERS_ENABLED to true.

        // If HttpRequestData is provided, use its URL; otherwise, get the baseUrl from the DurableTaskClient.
        // The base URL could be null if:
        // 1. The DurableTaskClient isn't a FunctionsDurableTaskClient (which would have the baseUrl from bindings)
        // 2. There's no valid HttpRequestData provided
        string? baseUrl = ((request != null) ? request.Url.GetLeftPart(UriPartial.Authority) : GetBaseUrl(client));

        if (baseUrl == null)
        {
            throw new InvalidOperationException("Failed to create HTTP management payload as base URL is null. Either use Functions bindings or provide an HTTP request to create the HttpPayload.");
        }
        
        bool isFromRequest = request != null;

        string formattedInstanceId = Uri.EscapeDataString(instanceId);

        // The baseUrl differs depending on the source. Eg:
        // - From request: http://localhost:7071/
        // - From durable client: http://localhost:7071/runtime/webhooks/durabletask
        // We adjust the instanceUrl construction accordingly.
        string instanceUrl = isFromRequest
            ? $"{baseUrl}/runtime/webhooks/durabletask/instances/{formattedInstanceId}"
            : $"{baseUrl}/instances/{formattedInstanceId}";
        string? commonQueryParameters = GetQueryParams(client);
        
        if (response != null)
        {
            response.Headers.Add("Location", BuildUrl(instanceUrl, commonQueryParameters));
            response.Headers.Add("Content-Type", "application/json");
        }

        return new HttpManagementPayload
        {
            Id = instanceId,
            PurgeHistoryDeleteUri = BuildUrl(instanceUrl, commonQueryParameters),
            SendEventPostUri = BuildUrl($"{instanceUrl}/raiseEvent/{{eventName}}", commonQueryParameters),
            StatusQueryGetUri = BuildUrl(instanceUrl, commonQueryParameters),
            TerminatePostUri = BuildUrl($"{instanceUrl}/terminate", "reason={{text}}", commonQueryParameters),
            SuspendPostUri =  BuildUrl($"{instanceUrl}/suspend", "reason={{text}}", commonQueryParameters),
            ResumePostUri =  BuildUrl($"{instanceUrl}/resume", "reason={{text}}", commonQueryParameters)
        };
    }

    private static ObjectSerializer GetObjectSerializer(HttpResponseData response)
    {
        return response.FunctionContext.InstanceServices.GetService<IOptions<WorkerOptions>>()?.Value?.Serializer
            ?? throw new InvalidOperationException("A serializer is not configured for the worker.");
    }

    private static string? GetQueryParams(DurableTaskClient client)
    {
        return client is FunctionsDurableTaskClient functions ? functions.QueryString : null;
    }

    private static string? GetBaseUrl(DurableTaskClient client)
    {
        return client is FunctionsDurableTaskClient functions ? functions.HttpBaseUrl : null;
    }
}
