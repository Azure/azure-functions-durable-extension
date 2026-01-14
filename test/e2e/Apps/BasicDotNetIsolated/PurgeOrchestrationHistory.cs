// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class PurgeOrchestrationHistory
{
    [Function(nameof(PurgeOrchestrationHistory))]
    public static async Task<HttpResponseData> PurgeHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        DateTime? purgeStartTime = null,
        DateTime? purgeEndTime = null,
        string? instanceId = null)
    {
        ILogger logger = executionContext.GetLogger("HelloCities_HttpStart");

        logger.LogInformation("Starting to purge instance histories");
        try
        {
            PurgeResult requestPurgeResult;
            if (!string.IsNullOrEmpty(instanceId))
            {
                // Purge a single instance
                requestPurgeResult = await client.PurgeInstanceAsync(instanceId);
                logger.LogInformation("Finished purging history for instance {instanceId}", instanceId);
            }
            else
            {
                // Purge by filter (terminal states only)
                requestPurgeResult = await client.PurgeAllInstancesAsync(new PurgeInstancesFilter(
                    purgeStartTime,
                    purgeEndTime,
                    [
                        OrchestrationRuntimeStatus.Completed,
                        OrchestrationRuntimeStatus.Failed,
                        OrchestrationRuntimeStatus.Terminated
                    ]));
                logger.LogInformation("Finished purge all instance history");
            }

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain");
            await response.WriteStringAsync($"Purged {requestPurgeResult.PurgedInstanceCount} records");
            return response;
        }
        catch (InvalidOperationException ex)
        {
            HttpResponseData response = req.CreateResponse(HttpStatusCode.PreconditionFailed);
            response.Headers.Add("Content-Type", "text/plain");
            await response.WriteStringAsync(ex.Message);
            return response;
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Failed to purge all instance history");
            HttpResponseData response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "text/plain");
            await response.WriteStringAsync($"Failed to purge all instance history: {ex.Message}");
            return response;
        }
    }

    [Function(nameof(InvokeDummyEntityOrchestration))]
    public static async Task<string> InvokeDummyEntityOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = new EntityInstanceId(nameof(DummyEntity), "myEntity");
        await context.Entities.CallEntityAsync(entityId, string.Empty);
        return "Success";
    }

    [Function(nameof(DummyEntity))]
    public static Task DummyEntity([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync(operation =>
        {
            operation.State.SetState("state");
            return new(0);
        });
    }
}
