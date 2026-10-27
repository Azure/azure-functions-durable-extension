// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using DurableTask.Core.History;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class ExternalizedPayloadFunctions
{
    [Function("ExternalizedPayload_Orchestrate")]
    public static Task<string> Orchestrate([OrchestrationTrigger] TaskOrchestrationContext context)
        => ExecuteAsync(context, context.GetInput<ExternalizedPayloadRequest>()!);

    internal static async Task<string> ExecuteAsync(TaskOrchestrationContext context, ExternalizedPayloadRequest request)
    {
        context.SetCustomStatus(request.Data);
        switch (request.Mode)
        {
            case "continue" when !request.Continued:
                context.ContinueAsNew(request with { Continued = true });
                return string.Empty;
            case "async":
                return (await context.CallActivityAsync<ExternalizedPayloadData>(
                    "ExternalizedPayload_EchoAsync", new ExternalizedPayloadData(request.Data))).Data;
            case "string":
                return await context.CallActivityAsync<string>("ExternalizedPayload_EchoString", request.Data);
            case "array":
                return string.Concat(await context.CallActivityAsync<string[]>(
                    "ExternalizedPayload_EchoArray", new[] { request.Data }));
            case "binary":
            case "registered-binary":
                byte[] bytes = await context.CallActivityAsync<byte[]>(
                    request.Mode == "binary" ? "ExternalizedPayload_EchoBinary" : nameof(ExternalizedRegisteredBinaryEcho),
                    Encoding.UTF8.GetBytes(request.Data));
                return Encoding.UTF8.GetString(bytes);
            case "registered":
                return (await context.CallActivityAsync<ExternalizedPayloadData>(
                    nameof(ExternalizedRegisteredEcho), new ExternalizedPayloadData(request.Data))).Data;
            case "sub":
                return await context.CallSubOrchestratorAsync<string>("ExternalizedPayload_Sub", request.Data);
            case "event":
                return await context.WaitForExternalEvent<string>("payload");
            case "http":
                var response = await context.CallHttpAsync(
                    HttpMethod.Post, request.EchoUri, request.Data, asynchronousPatternEnabled: false, tokenSource: null);
                return response.Content!;
            case "sync":
            case "continue":
            case "registered-orchestration":
                return (await context.CallActivityAsync<ExternalizedPayloadData>(
                    "ExternalizedPayload_Echo", new ExternalizedPayloadData(request.Data))).Data;
            default:
                throw new InvalidOperationException($"Unknown payload test mode '{request.Mode}'.");
        }
    }

    [Function("ExternalizedPayload_Sub")]
    public static string Sub([OrchestrationTrigger] TaskOrchestrationContext context) => context.GetInput<string>()!;

    [Function("ExternalizedPayload_Echo")]
    public static ExternalizedPayloadData Echo([ActivityTrigger] ExternalizedPayloadData input) => input;

    [Function("ExternalizedPayload_EchoAsync")]
    public static async Task<ExternalizedPayloadData> EchoAsync([ActivityTrigger] ExternalizedPayloadData input)
    {
        await Task.Yield();
        return input;
    }

    [Function("ExternalizedPayload_EchoString")]
    public static string EchoString([ActivityTrigger] string input) => input;

    [Function("ExternalizedPayload_EchoArray")]
    public static string[] EchoArray([ActivityTrigger] string[] input) => input;

    [Function("ExternalizedPayload_EchoBinary")]
    public static byte[] EchoBinary([ActivityTrigger] byte[] input) => input;

    [Function("ExternalizedPayload_Start")]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ExternalizedPayload_Start/{mode}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string mode)
    {
        int size = await req.ReadFromJsonAsync<int>();
        var request = new ExternalizedPayloadRequest(
            mode, new string('x', size), new Uri(req.Url, "/api/ExternalizedPayload_HttpEcho"));
        string name = mode == "registered-orchestration"
            ? nameof(ExternalizedRegisteredOrchestrator) : "ExternalizedPayload_Orchestrate";
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(name, request);
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("ExternalizedPayload_Event")]
    public static async Task<HttpResponseData> RaiseEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ExternalizedPayload_Event/{id}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string id)
    {
        string? data = await req.ReadFromJsonAsync<string>();
        await client.RaiseEventAsync(id, "payload", data);
        return req.CreateResponse(HttpStatusCode.Accepted);
    }

    [Function("ExternalizedPayload_HttpEcho")]
    public static async Task<HttpResponseData> HttpEcho(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        using var reader = new StreamReader(req.Body);
        string data = await reader.ReadToEndAsync();
        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(data);
        return response;
    }

    [Function("ExternalizedPayload_Read")]
    public static async Task<HttpResponseData> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ExternalizedPayload_Read/{id}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string id)
    {
        OrchestrationMetadata metadata = await client.GetInstancesAsync(id, getInputsAndOutputs: true)
            ?? throw new InvalidOperationException("Payload test instance was not found.");
        IList<HistoryEvent> history = await client.GetOrchestrationHistoryAsync(id);
        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(JsonSerializer.Serialize(new
        {
            input = metadata.ReadInputAs<ExternalizedPayloadRequest>()!.Data,
            output = metadata.ReadOutputAs<string>(),
            customStatus = metadata.ReadCustomStatusAs<string>(),
            activityOutputs = history.OfType<TaskCompletedEvent>().Select(completed => completed.Result).ToArray(),
        }));
        return response;
    }
}

public sealed record ExternalizedPayloadData(string Data);

public sealed record ExternalizedPayloadRequest(string Mode, string Data, Uri EchoUri, bool Continued = false);

public sealed class ExternalizedRegisteredEcho : TaskActivity<ExternalizedPayloadData, ExternalizedPayloadData>
{
    public override Task<ExternalizedPayloadData> RunAsync(TaskActivityContext context, ExternalizedPayloadData input)
        => Task.FromResult(input);
}

public sealed class ExternalizedRegisteredBinaryEcho : TaskActivity<byte[], byte[]>
{
    public override Task<byte[]> RunAsync(TaskActivityContext context, byte[] input) => Task.FromResult(input);
}

public sealed class ExternalizedRegisteredOrchestrator : TaskOrchestrator<ExternalizedPayloadRequest, string>
{
    public override Task<string> RunAsync(TaskOrchestrationContext context, ExternalizedPayloadRequest input)
        => ExternalizedPayloadFunctions.ExecuteAsync(context, input);
}
