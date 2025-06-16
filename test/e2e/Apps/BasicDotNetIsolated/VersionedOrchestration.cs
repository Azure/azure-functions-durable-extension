// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class VersionedOrchestration
{
    [Function(nameof(VersionedOrchestration))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(VersionedOrchestration));
        logger.LogInformation($"Versioned orchestration! Version: '{context.Version}'");

        return await context.CallActivityAsync<string>(nameof(SayVersion), context.Version);
    }

    [Function(nameof(RunWithSubOrchestrator))]
    public static async Task<string> RunWithSubOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        string? subVersion)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(VersionedOrchestration));
        logger.LogInformation($"Versioned orchestration! Version: '{context.Version}' Sub Version: '{subVersion}'");

        string subOrchestrationResponse;
        if (subVersion == null)
        {
            subOrchestrationResponse = await context.CallSubOrchestratorAsync<string>(nameof(VersionedSubOrchestration));
        }
        else
        {
            subOrchestrationResponse = await context.CallSubOrchestratorAsync<string>(nameof(VersionedSubOrchestration), new SubOrchestrationOptions
            {
                Version = subVersion,
            });
        }
        return $"Parent Version: '{context.Version}' | Sub {subOrchestrationResponse}";
    }

    [Function(nameof(VersionedSubOrchestration))]
    public static async Task<string> VersionedSubOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(VersionedOrchestration));
        logger.LogInformation($"Versioned sub-orchestration! Version: '{context.Version}'");

        return await context.CallActivityAsync<string>(nameof(SayVersion), context.Version);
    }

    [Function(nameof(SayVersion))]
    public static string SayVersion([ActivityTrigger] string version, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("SayVersion");
        logger.LogInformation("Activity running with version: {name}.", version);
        return $"Version: '{version}'";
    }

    [Function("OrchestrationVersion_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string? version)
    {
        ILogger logger = executionContext.GetLogger("VersionedOrchestration_HttpStart");

        // Function input comes from the request content.
        string instanceId;
        if (version != null)
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(VersionedOrchestration), new StartOrchestrationOptions
            {
                Version = version,
            });
        }
        else
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(VersionedOrchestration));
        }

        logger.LogInformation("Started orchestration with ID = '{instanceId}' and Version = '{version}'.", instanceId, version);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("OrchestrationSubVersion_HttpStart")]
    public static async Task<HttpResponseData> HttpSubStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string? subOrchestrationVersion)
    {
        ILogger logger = executionContext.GetLogger("OrchestrationSubVersion_HttpStart");

        // Function input comes from the request content.
        string instanceId;
        if (subOrchestrationVersion != null)
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(RunWithSubOrchestrator), input: subOrchestrationVersion);
        }
        else
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(RunWithSubOrchestrator));
        }

        logger.LogInformation("Started orchestration with ID = '{instanceId}' and Version = '{subOrchestrationVersion}'.", instanceId, subOrchestrationVersion);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
