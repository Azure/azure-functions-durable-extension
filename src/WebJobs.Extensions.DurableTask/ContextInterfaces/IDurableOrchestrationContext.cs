// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provides functionality available to orchestration code.
    /// </summary>
    public interface IDurableOrchestrationContext : IInterleavingContext
    {
        /// <summary>
        /// Gets the instance ID of the currently executing orchestration.
        /// </summary>
        /// <remarks>
        /// The instance ID is generated and fixed when the orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The ID of the current orchestration instance.
        /// </value>
        string InstanceId { get; }

        /// <summary>
        /// Gets the parent instance ID of the currently executing sub-orchestration.
        /// </summary>
        /// <remarks>
        /// The parent instance ID is generated and fixed when the parent orchestrator function is scheduled. It can be either
        /// auto-generated, in which case it is formatted as a GUID, or it can be user-specified with any format.
        /// </remarks>
        /// <value>
        /// The ID of the parent orchestration of the current sub-orchestration instance. The value will be available only in sub-orchestrations.
        /// </value>
        string ParentInstanceId { get; }

        /// <summary>
        /// Gets the input of the current orchestrator function as a deserialized value.
        /// </summary>
        /// <typeparam name="TInput">Any data contract type that matches the JSON input.</typeparam>
        /// <returns>The deserialized input value.</returns>
        TInput GetInput<TInput>();

        /// <summary>
        /// Restarts the orchestration by clearing its history.
        /// </summary>
        /// <remarks>
        /// <para>Large orchestration histories can consume a lot of memory and cause delays in
        /// instance load times. This method can be used to periodically truncate the stored
        /// history of an orchestration instance.</para>
        /// <para>Note that any unprocessed external events will be discarded when an orchestration
        /// instance restarts itself using this method.</para>
        /// </remarks>
        /// <param name="input">The JSON-serializeable data to re-initialize the instance with.</param>
        /// <param name="preserveUnprocessedEvents">
        /// If set to <c>true</c>, re-adds any unprocessed external events into the new execution
        /// history when the orchestration instance restarts. If <c>false</c>, any unprocessed
        /// external events will be discarded when the orchestration instance restarts.
        /// </param>
        void ContinueAsNew(object input, bool preserveUnprocessedEvents = false);

        /// <summary>
        /// Sets the JSON-serializeable status of the current orchestrator function.
        /// </summary>
        /// <remarks>
        /// The <paramref name="customStatusObject"/> value is serialized to JSON and will
        /// be made available to the orchestration status query APIs. The serialized JSON
        /// value must not exceed 16 KB of UTF-16 encoded text.
        /// </remarks>
        /// <param name="customStatusObject">The JSON-serializeable value to use as the orchestrator function's custom status.</param>
        void SetCustomStatus(object customStatusObject);

        /// <summary>
        /// Makes an HTTP call to the specified uri.
        /// </summary>
        /// <param name="method">HttpMethod used for api call.</param>
        /// <param name="uri">uri used to make the HTTP call.</param>
        /// <param name="content">Content passed in the HTTP request.</param>
        /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
        Task<DurableHttpResponse> CallHttpAsync(HttpMethod method, Uri uri, string content = null);

        /// <summary>
        /// Makes an HTTP call using the information in the DurableHttpRequest.
        /// </summary>
        /// <param name="req">The DurableHttpRequest used to make the HTTP call.</param>
        /// <returns>A <see cref="Task{DurableHttpResponse}"/>Result of the HTTP call.</returns>
        Task<DurableHttpResponse> CallHttpAsync(DurableHttpRequest req);
    }
}
