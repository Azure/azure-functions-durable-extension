// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using System.Net;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class ExternalEventOrchestration
{
    [Function(nameof(ExternalEventOrchestrator))]
    public static async Task<string> ExternalEventOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        bool approval = await context.WaitForExternalEvent<bool>("Approval", CancellationToken.None);

        return "Orchestrator Finished!";
    }

    [Function("SendExternalEvent_HttpStart")]
    public static async Task<HttpResponseData> SendExternalEvent_HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        string? instanceId = await req.ReadFromJsonAsync<string>();
        var response = req.CreateResponse();

        try
        {
            await client.RaiseEventAsync(instanceId!, "Approval", true);
            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync($"External event sent to {instanceId}.");
        }
        catch (RpcException ex)
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync($"gRPC error: {ex.StatusCode} - {ex.Message}");
        }

        return response;
    }
}
