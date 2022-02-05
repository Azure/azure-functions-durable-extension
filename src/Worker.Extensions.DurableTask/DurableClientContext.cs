// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using DurableTask;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Http;

namespace Microsoft.Azure.Functions.Worker;

// NOTE: This could have been an interface, but an abstract class was chosen to avoid breaking
//       custom test implementations when new properties or methods are added.

/// <summary>
/// Defines properties and methods for interacting with the Durable Functions Client binding.
/// </summary>
[InputConverter(typeof(DefaultDurableClientContext.Converter))]
public abstract class DurableClientContext
{
    /// <summary>
    /// Gets the durable task client associated with the current function.
    /// </summary>
    public abstract DurableTaskClient Client { get; }

    /// <summary>
    /// Gets the name of the client binding's task hub.
    /// </summary>
    public abstract string TaskHubName { get; }

    /// <summary>
    /// Creates an HTTP response that is useful for checking the status of the specified instance.
    /// </summary>
    /// <remarks>
    /// The payload of the returned <see cref="HttpResponseData"/> contains HTTP API URLs that can
    /// be used to query the status of the orchestration, raise events to the orchestration, or
    /// terminate the orchestration.
    /// </remarks>
    /// <param name="request">The HTTP request that triggered the current orchestration instance.</param>
    /// <param name="instanceId">The ID of the orchestration instance to check.</param>
    /// <param name="returnInternalServerErrorOnFailure">Optional parameter that configures the http response code returned. Defaults to <c>false</c>.
    /// If <c>true</c>, the returned http response code will be a 500 when the orchestrator is in a failed state. If <c>false</c> the response will
    /// return 200 and the caller will be expected to parse the payload to determine whether the orchestration succeeded or failed.</param>
    /// <returns>An HTTP 202 response with a Location header and a payload containing instance control URLs.</returns>
    public abstract HttpResponseData CreateCheckStatusResponse(HttpRequestData request, string instanceId, bool returnInternalServerErrorOnFailure = false);
}
