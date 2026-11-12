// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
// This is a copy of: https://github.com/Azure/azure-functions-durable-extension/blob/dev/src/WebJobs.Extensions.DurableTask/HttpManagementPayload.cs

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.Functions.Worker;

/// <summary>
/// Data structure containing status, terminate and send external event HTTP endpoints.
/// </summary>
public class HttpManagementPayload
{
    /// <summary>
    /// Gets the ID of the orchestration instance.
    /// </summary>
    /// <value>
    /// The ID of the orchestration instance.
    /// </value>
    [JsonProperty("id")]
    public string? Id { get; internal set; }

    /// <summary>
    /// Gets the HTTP GET status query endpoint URL.
    /// </summary>
    /// <value>
    /// The HTTP URL for fetching the instance status.
    /// </value>
    [JsonProperty("statusQueryGetUri")]
    public string? StatusQueryGetUri { get; internal set; }

    /// <summary>
    /// Gets the HTTP POST external event sending endpoint URL.
    /// </summary>
    /// <value>
    /// The HTTP URL for posting external event notifications.
    /// </value>
    [JsonProperty("sendEventPostUri")]
    public string? SendEventPostUri { get; internal set; }

    /// <summary>
    /// Gets the HTTP POST instance termination endpoint.
    /// </summary>
    /// <value>
    /// The HTTP URL for posting instance termination commands.
    /// </value>
    [JsonProperty("terminatePostUri")]
    public string? TerminatePostUri { get; internal set; }

    /// <summary>
    /// Gets the HTTP POST instance rewind endpoint.
    /// </summary>
    /// <value>
    /// The HTTP URL for rewinding orchestration instances.
    /// </value>
    [JsonProperty("rewindPostUri")]
    public string? RewindPostUri { get; internal set; }

    /// <summary>
    /// Gets the HTTP DELETE purge instance history by instance ID endpoint.
    /// </summary>
    /// <value>
    /// The HTTP URL for purging instance history by instance ID.
    /// </value>
    [JsonProperty("purgeHistoryDeleteUri")]
    public string? PurgeHistoryDeleteUri { get; internal set; }

    /// <summary>
    /// Gets the HTTP POST instance restart endpoint.
    /// </summary>
    /// <value>
    /// The HTTP URL for restarting an orchestration instance.
    /// </value>
    [JsonProperty("restartPostUri")]
    public string? RestartPostUri { get; internal set; }

    /// <summary>
    /// Gets the HTTP POST instance suspend endpoint.
    /// </summary>
    /// <value>
    /// The HTTP URL for suspending an orchestration instance.
    /// </value>
    [JsonProperty("suspendPostUri")]
    public string? SuspendPostUri { get; internal set; }

    /// <summary>
    /// Gets the HTTP POST instance resume endpoint.
    /// </summary>
    /// <value>
    /// The HTTP URL for resuming an orchestration instance.
    /// </value>
    [JsonProperty("resumePostUri")]
    public string? ResumePostUri { get; internal set; }
}
