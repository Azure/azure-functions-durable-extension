// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class HelloCities
{
    [Function(nameof(HelloCities))]
    public static async Task<List<string>> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(HelloCities));
        logger.LogInformation("Saying hello.");
        var outputs = new List<string>();

        // Replace name and input with values relevant for your Durable Functions Activity
        outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
        outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
        outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));

        // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
        return outputs;
    }

    [Function(nameof(SayHello))]
    public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("SayHello");
        logger.LogInformation("Saying hello to {name}.", name);
        return $"Hello {name}!";
    }

    [Function(nameof(StartOrchestration))]
    public static async Task<HttpResponseData> StartOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string orchestrationName,
        string? instanceId)
    {
        ILogger logger = executionContext.GetLogger(nameof(StartOrchestration));

        // Function input comes from the request content.
        instanceId = await client.ScheduleNewOrchestrationInstanceAsync(orchestrationName, new StartOrchestrationOptions(InstanceId: instanceId));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("HelloCities_HttpStart_Scheduled")]
    public static async Task<HttpResponseData> HttpStartScheduled(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        DateTime scheduledStartTime,
        string? instanceId)
    {
        ILogger logger = executionContext.GetLogger("HelloCities_HttpStart");

        var startOptions = new StartOrchestrationOptions(StartAt: scheduledStartTime, InstanceId: instanceId);

        // Function input comes from the request content.
        instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(HelloCities), startOptions);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(StartOrchestration_DedupeStatuses))]
    public static async Task<HttpResponseData> StartOrchestration_DedupeStatuses(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string orchestrationName,
        string instanceId,
        string[] dedupeStatuses,
        DateTime? scheduledStartTime)
    {
        ILogger logger = executionContext.GetLogger(nameof(StartOrchestration_DedupeStatuses));

        StartOrchestrationOptions startOptions = new(InstanceId: instanceId);

        var parsedStatuses = new OrchestrationRuntimeStatus[dedupeStatuses.Length];
        for (int i = 0; i < dedupeStatuses.Length; i++)
        {
            string statusStr = dedupeStatuses[i];
            if (!Enum.TryParse<OrchestrationRuntimeStatus>(statusStr, ignoreCase: true, out var status))
            {
                throw new ArgumentException($"Invalid OrchestrationRuntimeStatus value: '{statusStr}'", nameof(dedupeStatuses));
            }
            parsedStatuses[i] = status;
        }
        startOptions = startOptions.WithDedupeStatuses(parsedStatuses);

        if (scheduledStartTime is not null)
        {
            startOptions = startOptions with { StartAt = scheduledStartTime };
        }

        // Function input comes from the request content.
        try
        {
            await client.ScheduleNewOrchestrationInstanceAsync(orchestrationName, startOptions);
        }
        catch (OrchestrationAlreadyExistsException ex)
        {
            // Tests expect Conflict (409) for orchestration dedupe scenarios.
            HttpResponseData response = req.CreateResponse(HttpStatusCode.Conflict);
            await response.WriteStringAsync(ex.Message);
            return response;
        }
        catch (ArgumentException ex)
        {
            // Tests expect BadRequest for invalid dedupe statuses
            HttpResponseData response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync(ex.Message);
            return response;
        }

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
