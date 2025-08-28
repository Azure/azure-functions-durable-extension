// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
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
    /// Waits for the completion of the specified orchestration instance with a retry interval, controlled by the cancellation token.
    /// If the orchestration does not complete within the required time, returns an HTTP response containing the <see cref="HttpManagementPayload"/> class to manage instances.
    /// </summary>
    /// <param name="client">The <see cref="DurableTaskClient"/>.</param>
    /// <param name="request">The HTTP request that this response is for.</param>
    /// <param name="instanceId">The ID of the orchestration instance to check.</param>
    /// <param name="retryInterval">The timeout between checks for output from the durable function. The default value is 1 second.</param>
    /// <param name="returnInternalServerErrorOnFailure">Optional parameter that configures the http response code returned. Defaults to <c>false</c>.</param>
    /// <param name="getInputsAndOutputs">Optional parameter that configures whether to get the inputs and outputs of the orchestration. Defaults to <c>false</c>.</param>
    /// <param name="cancellation">A token that signals if the wait should be canceled. If canceled, call CreateCheckStatusResponseAsync to return a reponse contains a HttpManagementPayload.</param>
    /// <returns></returns>
    public static async Task<HttpResponseData> WaitForCompletionOrCreateCheckStatusResponseAsync(
        this DurableTaskClient client,
        HttpRequestData request,
        string instanceId,
        TimeSpan? retryInterval = null,
        bool returnInternalServerErrorOnFailure = false,
        bool getInputsAndOutputs = false,
        CancellationToken cancellation = default
    )
    {
        TimeSpan retryIntervalLocal = retryInterval ?? TimeSpan.FromSeconds(1);
        try
        {
            while (true)
            {
                var status = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: getInputsAndOutputs);
                if (status != null)
                {
                    if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
#pragma warning disable CS0618 // Type or member is obsolete
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Canceled ||
#pragma warning restore CS0618 // Type or member is obsolete
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated ||
                        status.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                    {
                        var response = request.CreateResponse(
                            (status.RuntimeStatus == OrchestrationRuntimeStatus.Failed && returnInternalServerErrorOnFailure) ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
                        await response.WriteAsJsonAsync(new
                        {
                            Name = status.Name,
                            InstanceId = status.InstanceId,
                            CreatedAt = status.CreatedAt,
                            LastUpdatedAt = status.LastUpdatedAt,
                            RuntimeStatus = status.RuntimeStatus.ToString(), // Convert enum to string
                            SerializedInput = status.SerializedInput,
                            SerializedOutput = status.SerializedOutput,
                            SerializedCustomStatus = status.SerializedCustomStatus
                        }, cancellation);

                        return response;
                    }
                }
                await Task.Delay(retryIntervalLocal, cancellation);
            }
        }
        // If the task is canceled, call CreateCheckStatusResponseAsync to return a response containing instance management URLs.
        catch (OperationCanceledException)
        {
            return await CreateCheckStatusResponseAsync(client, request, instanceId);
        }
     }

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

        // If HttpRequestData is provided, use its URL; otherwise, get the baseUrl from the DurableTaskClient.
        // The base URL could be null if:
        // 1. The DurableTaskClient isn't a FunctionsDurableTaskClient (which would have the baseUrl from bindings)
        // 2. There's no valid HttpRequestData provided
        string? baseUrl = request != null ? GetBaseUrlFromRequest(request) : GetBaseUrl(client);

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

    private static string? GetBaseUrlFromRequest(HttpRequestData request)
    {
        // Default to the scheme from the request URL
        string proto = request.Url.Scheme;
        string host = request.Url.Authority;

        // Check for "Forwarded" header
        if (request.Headers.TryGetValues("Forwarded", out var forwardedHeaders))
        {
            var forwardedDict = forwardedHeaders.FirstOrDefault()?.Split(';')
                .Select(pair => pair.Split('='))
                .Where(pair => pair.Length == 2)
                .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim());

            if (forwardedDict != null)
            {
                if (forwardedDict.TryGetValue("proto", out var forwardedProto))
                {
                    proto = forwardedProto;
                }
                if (forwardedDict.TryGetValue("host", out var forwardedHost))
                {
                    host = forwardedHost;
                    // Return if either proto or host (or both) were found in "Forwarded" header
                    return $"{proto}://{forwardedHost}";
                }
            }
        }

        // Check for "X-Forwarded-Proto" and "X-Forwarded-Host" headers if "Forwarded" is not present
        if (request.Headers.TryGetValues("X-Forwarded-Proto", out var protos))
        {
            proto = protos.FirstOrDefault() ?? proto;
        }
        
        if (request.Headers.TryGetValues("X-Forwarded-Host", out var hosts))
        {
            // Return base URL if either "X-Forwarded-Proto" or "X-Forwarded-Host" (or both) are found
            host = hosts.FirstOrDefault() ?? host;
            return $"{proto}://{host}";
        }

        // Construct and return the base URL from default fallback values
        return $"{proto}://{host}";
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
