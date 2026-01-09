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

        string baseUrl = GetBaseUrl(request);
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
            suspendPostUri = BuildUrl($"{instanceUrl}/suspend", "reason={{text}}", commonQueryParameters),
            resumePostUri = BuildUrl($"{instanceUrl}/resume", "reason={{text}}", commonQueryParameters)
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

    /// <summary>
    /// Extracts the base URL from the request, taking into account forwarded headers
    /// for scenarios involving proxies or application gateways.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The base URL including scheme and authority.</returns>
    /// <remarks>
    /// This method checks headers in the following order:
    /// 1. Standard "Forwarded" header (RFC 7239)
    /// 2. "X-Forwarded-Host" and "X-Forwarded-Proto" headers
    /// 3. Falls back to the original request URL.
    /// Security: Host and protocol values are validated to prevent header injection attacks.
    /// </remarks>
    internal static string GetBaseUrl(HttpRequestData request)
    {
        // Check for standard Forwarded header (RFC 7239)
        // Format: Forwarded: host=example.com;proto=https
        if (request.Headers.TryGetValues("Forwarded", out var forwardedValues))
        {
            foreach (string forwarded in forwardedValues)
            {
                if (string.IsNullOrEmpty(forwarded))
                {
                    continue;
                }

                string? host = null;
                string? proto = null;

                // Parse the Forwarded header - directives are separated by semicolons
                // Multiple proxies are separated by commas; we use the first (leftmost) entry
                string firstEntry = forwarded.Split(',')[0];
                string[] parts = firstEntry.Split(';');

                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (trimmed.StartsWith("host=", StringComparison.OrdinalIgnoreCase))
                    {
                        host = trimmed.Substring(5).Trim('"');
                    }
                    else if (trimmed.StartsWith("proto=", StringComparison.OrdinalIgnoreCase))
                    {
                        proto = trimmed.Substring(6).Trim('"');
                    }
                }

                if (!string.IsNullOrEmpty(host) && IsValidHost(host!))
                {
                    proto = GetValidatedProtocol(proto, request.Url.Scheme);
                    return $"{proto}://{host}";
                }

            }
        }

        // Check for X-Forwarded-Host and X-Forwarded-Proto headers
        string? forwardedHost = null;
        string? forwardedProto = null;

        if (request.Headers.TryGetValues("X-Forwarded-Host", out var hostValues))
        {
            foreach (string hostValue in hostValues)
            {
                // X-Forwarded-Host can contain multiple values separated by commas
                // Use the first (leftmost) value which represents the original client request
                string candidate = hostValue.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(candidate) && IsValidHost(candidate))
                {
                    forwardedHost = candidate;
                    break;
                }
            }
        }

        if (request.Headers.TryGetValues("X-Forwarded-Proto", out var protoValues))
        {
            foreach (string protoValue in protoValues)
            {
                forwardedProto = protoValue.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(forwardedProto))
                {
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(forwardedHost))
        {
            forwardedProto = GetValidatedProtocol(forwardedProto, request.Url.Scheme);
            return $"{forwardedProto}://{forwardedHost}";
        }

        // Fall back to the original request URL
        return request.Url.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>
    /// Validates and returns a safe protocol value.
    /// Only allows "http" or "https" to prevent protocol injection attacks.
    /// </summary>
    /// <param name="protocol">The protocol to validate.</param>
    /// <param name="fallback">The fallback protocol if validation fails.</param>
    /// <returns>A validated protocol string.</returns>
    private static string GetValidatedProtocol(string? protocol, string fallback)
    {
        if (string.IsNullOrEmpty(protocol))
        {
            return fallback;
        }

        // Only allow http or https to prevent protocol injection
        if (protocol!.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            protocol.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return protocol.ToLowerInvariant();
        }

        return fallback;
    }

    /// <summary>
    /// Validates that a host value is safe to use in URL construction.
    /// Prevents host header injection attacks by rejecting malformed or malicious host values.
    /// </summary>
    /// <param name="host">The host value to validate.</param>
    /// <returns>True if the host is valid, false otherwise.</returns>
    private static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // Reject hosts containing characters that could be used for injection attacks
        // These characters should never appear in a valid host:
        // - Path separators (/, \)
        // - Query/fragment markers (?, #)
        // - Whitespace or control characters
        // - URL encoding markers (%)
        // - Characters that could break URL structure (@, <, >, ", ')
        foreach (char c in host)
        {
            if (c == '/' || c == '\\' || c == '?' || c == '#' ||
                c == '@' || c == '<' || c == '>' || c == '"' || c == '\'' ||
                c == '%' || char.IsControl(c) || char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        // Attempt to construct a URI to validate the host format
        // This catches malformed hosts that passed the character check
        if (Uri.TryCreate($"https://{host}/", UriKind.Absolute, out Uri? testUri))
        {
            // Ensure the host wasn't interpreted differently than intended
            // At this point, characters such as '@' have already been rejected by the
            // validation loop above. Here we ensure that Uri parsing did not reinterpret
            // a valid-looking host (optionally with port) into a different host/port pair.
            // Detect if the input host contains a port specification
            // For IPv6, the port comes after the closing bracket: [::1]:8080
            // For IPv4/hostname, any colon indicates a port: example.com:8080
            int lastBracket = host.LastIndexOf(']');
            int lastColon = host.LastIndexOf(':');
            bool hostHasPort = lastColon > lastBracket;

            string constructedHost;
            if (hostHasPort && testUri.Port != -1)
            {
                // Input host included a port, so include it in comparison
                constructedHost = $"{testUri!.Host}:{testUri.Port}";
            }
            else
            {
                // No port in input, compare host only
                constructedHost = testUri!.Host;
            }

            return string.Equals(host, constructedHost, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
