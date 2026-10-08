// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

public static class PurgeEndpoints
{
    [Function(nameof(GetPayloadStorage))]
    public static async Task<HttpResponseData> GetPayloadStorage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "large-payload-purge/storage")] HttpRequestData request)
    {
        var service = new BlobServiceClient(Environment.GetEnvironmentVariable("PurgePayloadConnection"));
        if (!service.Uri.IsLoopback)
        {
            throw new InvalidOperationException("Storage evidence is restricted to the local E2E emulator.");
        }

        BlobContainerClient container = service.GetBlobContainerClient(Environment.GetEnvironmentVariable("PurgePayloadContainer"));
        var blobs = new List<object>();
        if (await container.ExistsAsync(request.FunctionContext.CancellationToken))
        {
            await foreach (BlobItem blob in container.GetBlobsAsync(
                new GetBlobsOptions { Traits = BlobTraits.Metadata }, cancellationToken: request.FunctionContext.CancellationToken))
            {
                blobs.Add(new { blob.Name, blob.Metadata, blob.Properties.ETag, blob.Properties.ContentLength });
            }
        }

        return await JsonResponse(request, new { blobs, workerPid = Environment.ProcessId });
    }

    [Function(nameof(SetPurge))]
    public static async Task<HttpResponseData> SetPurge(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "large-payload-purge/{enabled:bool}")] HttpRequestData request,
        [DurableClient] DurableTaskClient client, bool enabled)
    {
        await client.SetLargePayloadAutoPurgeAsync(enabled, cancellationToken: request.FunctionContext.CancellationToken);
        return await JsonResponse(request, new { enabled, workerPid = Environment.ProcessId });
    }

    [Function(nameof(SetPurgeOverride))]
    public static async Task<HttpResponseData> SetPurgeOverride(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "large-payload-purge/override/{enabled:bool}")] HttpRequestData request,
        [DurableClient(TaskHub = "%PurgeOverrideHub%", ConnectionName = "PurgeOverrideConnection")] DurableTaskClient client, bool enabled)
    {
        await client.SetLargePayloadAutoPurgeAsync(enabled, cancellationToken: request.FunctionContext.CancellationToken);
        return await JsonResponse(request, new { enabled, workerPid = Environment.ProcessId });
    }

    [Function(nameof(StartPayload))]
    public static async Task<HttpResponseData> StartPayload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "large-payload-purge/payload")] HttpRequestData request,
        [DurableClient] DurableTaskClient client)
    {
        string id = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(PayloadOrchestration), new string('x', 384 * 1024),
            new StartOrchestrationOptions { Version = "2.0" }, request.FunctionContext.CancellationToken);
        return await JsonResponse(request, new { instanceId = id, workerPid = Environment.ProcessId });
    }

    [Function(nameof(PayloadOrchestration))]
    public static Task<string> PayloadOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
        => context.CallActivityAsync<string>(nameof(EchoPayload), context.GetInput<string>());

    [Function(nameof(StartMismatchedVersion))]
    public static async Task<HttpResponseData> StartMismatchedVersion(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "large-payload-purge/mismatched-version")] HttpRequestData request,
        [DurableClient] DurableTaskClient client)
    {
        string id = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(PayloadOrchestration), "business-version-check",
            new StartOrchestrationOptions { Version = "1.0" }, request.FunctionContext.CancellationToken);
        return await JsonResponse(request, new { instanceId = id, workerPid = Environment.ProcessId });
    }

    [Function(nameof(EchoPayload))]
    public static string EchoPayload([ActivityTrigger] string input) => input;

    [Function(nameof(PurgeInstance))]
    public static async Task<HttpResponseData> PurgeInstance(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "large-payload-purge/instances/{id}/purge")] HttpRequestData request,
        [DurableClient] DurableTaskClient client, string id)
    {
        PurgeResult result = await client.PurgeInstanceAsync(id, cancellation: request.FunctionContext.CancellationToken);
        return await JsonResponse(request, new { result.PurgedInstanceCount, workerPid = Environment.ProcessId });
    }

    [Function(nameof(GetInstance))]
    public static async Task<HttpResponseData> GetInstance(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "large-payload-purge/instances/{id}")] HttpRequestData request,
        [DurableClient] DurableTaskClient client, string id)
    {
        OrchestrationMetadata? state = await client.GetInstancesAsync(id, getInputsAndOutputs: true, request.FunctionContext.CancellationToken);
        return await JsonResponse(request, new
        {
            instanceId = id,
            status = state?.RuntimeStatus.ToString(),
            customStatus = state?.SerializedCustomStatus,
            failure = state?.FailureDetails?.ErrorMessage,
            workerPid = Environment.ProcessId,
        });
    }

    private static async Task<HttpResponseData> JsonResponse(HttpRequestData request, object result)
    {
        HttpResponseData response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }
}
