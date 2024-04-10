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

        HttpResponseData response = request.CreateResponse(statusCode);
        object payload = SetHeadersAndGetPayload(client, request, response, instanceId);

        ObjectSerializer serializer = GetObjectSerializer(response);
        serializer.Serialize(response.Body, payload, payload.GetType(), cancellation);
        return response;
    }

    private static object SetHeadersAndGetPayload(
        DurableTaskClient client, HttpRequestData request, HttpResponseData response, string instanceId)
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
        string baseUrl = request.Url.GetLeftPart(UriPartial.Authority);
        string formattedInstanceId = Uri.EscapeDataString(instanceId);
        string instanceUrl = $"{baseUrl}/runtime/webhooks/durabletask/instances/{formattedInstanceId}";
        string? commonQueryParameters = GetQueryParams(client);
        response.Headers.Add("Location", BuildUrl(instanceUrl, commonQueryParameters));
        response.Headers.Add("Content-Type", "application/json");

        return new
        {
            id = instanceId,
            purgeHistoryDeleteUri = BuildUrl(instanceUrl, commonQueryParameters),
            sendEventPostUri = BuildUrl($"{instanceUrl}/raiseEvent/{{eventName}}", commonQueryParameters),
            statusQueryGetUri = BuildUrl(instanceUrl, commonQueryParameters),
            terminatePostUri = BuildUrl($"{instanceUrl}/terminate", "reason={{text}}", commonQueryParameters),
            suspendPostUri =  BuildUrl($"{instanceUrl}/suspend", "reason={{text}}", commonQueryParameters),
            resumePostUri =  BuildUrl($"{instanceUrl}/resume", "reason={{text}}", commonQueryParameters)
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
}
