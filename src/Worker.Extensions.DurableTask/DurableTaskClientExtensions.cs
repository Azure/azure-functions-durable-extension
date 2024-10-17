// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
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
    /// 
    /// </summary>
    /// <param name="client">The <see cref="DurableTaskClient"/>.</param>
    /// <param name="request">The HTTP request that this response is for.</param>
    /// <param name="instanceId">The ID of the orchestration instance to check.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <param name="timeout">Total allowed timeout for output from the durable function. The default value is 10 seconds.</param>
    /// <param name="retryInterval">The timeout between checks for output from the durable function. The default value is 1 second.</param>
    /// <param name="returnInternalServerErrorOnFailure">Optional parameter that configures the http response code returned. Defaults to <c>false</c>.
    /// <returns></returns>
    public static async Task<HttpResponseData> WaitForCompletionOrCreateCheckStatusResponseAsync(this DurableTaskClient client,
        HttpRequestData request,
        string instanceId,
        CancellationToken cancellation = default,
        TimeSpan? timeout = null,
        TimeSpan? retryInterval = null,
        bool returnInternalServerErrorOnFailure = false
    )
    {
        var timeoutLocal = timeout ?? TimeSpan.FromSeconds(10);
        var retryIntervalLocal = retryInterval ?? TimeSpan.FromSeconds(1);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var status = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);
            if (status != null)
            {
                if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
#pragma warning disable CS0618 // Type or member is obsolete
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Canceled ||
#pragma warning restore CS0618 // Type or member is obsolete
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Failed)
                {
                    var response = request.CreateResponse((status.RuntimeStatus == OrchestrationRuntimeStatus.Failed && returnInternalServerErrorOnFailure) ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new
                    {
                        name = status.Name,
                        instanceId = status.InstanceId,
                        runtimeStatus = status.RuntimeStatus.ToString(),
                        input = status.ReadInputAs<object?>(),
                        customStatus = status.ReadCustomStatusAs<object?>(),
                        output = status.ReadOutputAs<object?>(),
                        createdTime = status.CreatedAt.ToString("s") + "Z",
                        lastUpdatedTime = status.LastUpdatedAt.ToString("s") + "Z",
                    });

                    return response;
                }
            }

            TimeSpan elapsed = stopwatch.Elapsed;
            if (elapsed < timeoutLocal)
            {
                TimeSpan remainingTime = timeoutLocal.Subtract(elapsed);
                await Task.Delay(remainingTime > retryIntervalLocal ? retryIntervalLocal : remainingTime);
            }
            else
            {
                return await CreateCheckStatusResponseAsync(client, request, instanceId, cancellation: cancellation, returnInternalServerErrorOnFailure: returnInternalServerErrorOnFailure);
            }
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
        CancellationToken cancellation = default,
        bool returnInternalServerErrorOnFailure = false)
    {
        return client.CreateCheckStatusResponseAsync(request, instanceId, HttpStatusCode.Accepted, cancellation, returnInternalServerErrorOnFailure: returnInternalServerErrorOnFailure);
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
        CancellationToken cancellation = default,
        bool returnInternalServerErrorOnFailure = false)
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
        object payload = SetHeadersAndGetPayload(client, request, response, instanceId, returnInternalServerErrorOnFailure: returnInternalServerErrorOnFailure);

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
        CancellationToken cancellation = default,
        bool returnInternalServerErrorOnFailure = false)
    {
        return client.CreateCheckStatusResponse(request, instanceId, HttpStatusCode.Accepted, cancellation, returnInternalServerErrorOnFailure: returnInternalServerErrorOnFailure);
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
        CancellationToken cancellation = default,
        bool returnInternalServerErrorOnFailure = false)
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
        object payload = SetHeadersAndGetPayload(client, request, response, instanceId, returnInternalServerErrorOnFailure: returnInternalServerErrorOnFailure);

        ObjectSerializer serializer = GetObjectSerializer(response);
        serializer.Serialize(response.Body, payload, payload.GetType(), cancellation);
        return response;
    }

    private static object SetHeadersAndGetPayload(
        DurableTaskClient client, HttpRequestData request, HttpResponseData response, string instanceId, bool returnInternalServerErrorOnFailure = false)
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
        string proto = request.Url.Scheme;
        if (request.Headers.TryGetValues("Forwarded", out var forwarded))
        {
            var forwardedDict = (forwarded.FirstOrDefault() ?? "").Split(';').Select(pair => pair.Split('=')).Select(pair => new { key = pair[0], value = pair[1] }).ToDictionary(pair => pair.key, pair => pair.value);
            if (forwardedDict.ContainsKey("proto"))
            {
                proto = forwardedDict["proto"];
            }

            if (forwardedDict.ContainsKey("host"))
            {
                baseUrl = $"{proto}://{forwardedDict["host"]}";
            }
        }
        else
        {
            if (request.Headers.TryGetValues("X-Forwarded-Proto", out var protos))
            {
                proto = protos.First();
            }

            if (request.Headers.TryGetValues("X-Forwarded-Host", out var hosts))
            {
                baseUrl = $"{proto}://{hosts.First()}";
            }
        }

        string formattedInstanceId = Uri.EscapeDataString(instanceId);
        string instanceUrl = $"{baseUrl}/runtime/webhooks/durabletask/instances/{formattedInstanceId}";
        string? commonQueryParameters = GetQueryParams(client);
        response.Headers.Add("Location", BuildUrl(instanceUrl, commonQueryParameters, returnInternalServerErrorOnFailure ? "returnInternalServerErrorOnFailure=true" : ""));
        response.Headers.Add("Content-Type", "application/json");

        return new
        {
            id = instanceId,
            purgeHistoryDeleteUri = BuildUrl(instanceUrl, commonQueryParameters),
            sendEventPostUri = BuildUrl($"{instanceUrl}/raiseEvent/{{eventName}}", commonQueryParameters),
            statusQueryGetUri = BuildUrl(instanceUrl, commonQueryParameters, returnInternalServerErrorOnFailure ? "returnInternalServerErrorOnFailure=true" : ""),
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
