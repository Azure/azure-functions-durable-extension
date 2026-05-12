// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace WorkItemFiltersSample;

// =============================================================================
// Orchestrations
// =============================================================================

/// <summary>
/// A simple orchestration that calls an activity and returns the result.
/// With work item filtering enabled, this orchestration will only be dispatched
/// to workers that have it registered.
/// </summary>
public static class GreetingOrchestration
{
    [Function(nameof(GreetingOrchestration) + "_Start")]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/greeting")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(GreetingOrchestration));
        return await HttpHelpers.CreateAcceptedResponse(req, instanceId);
    }

    [Function(nameof(GreetingOrchestration))]
    public static async Task<string> Run([OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger(nameof(GreetingOrchestration));
        logger.LogInformation("GreetingOrchestration started");
        string result = await ctx.CallActivityAsync<string>(nameof(SayHello), "World");
        return result;
    }
}

/// <summary>
/// A fan-out/fan-in orchestration that calls the same activity in parallel with
/// different inputs. Demonstrates that activity work items are also filtered —
/// only workers that register the activity will receive the dispatched work.
/// </summary>
public static class FanOutOrchestration
{
    [Function(nameof(FanOutOrchestration) + "_Start")]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/fanout")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(FanOutOrchestration));
        return await HttpHelpers.CreateAcceptedResponse(req, instanceId);
    }

    [Function(nameof(FanOutOrchestration))]
    public static async Task<string[]> Run([OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger(nameof(FanOutOrchestration));
        logger.LogInformation("FanOutOrchestration: fanning out to 3 activities");

        string[] results = await Task.WhenAll(
            ctx.CallActivityAsync<string>(nameof(SayHello), "Tokyo"),
            ctx.CallActivityAsync<string>(nameof(SayHello), "London"),
            ctx.CallActivityAsync<string>(nameof(SayHello), "Seattle"));

        return results;
    }
}

/// <summary>
/// A parent orchestration that calls a child orchestration. Demonstrates that
/// sub-orchestration dispatch is also governed by work item filters.
/// </summary>
public static class ParentOrchestration
{
    [Function(nameof(ParentOrchestration) + "_Start")]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/parent")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(ParentOrchestration));
        return await HttpHelpers.CreateAcceptedResponse(req, instanceId);
    }

    [Function(nameof(ParentOrchestration))]
    public static async Task<string> Run([OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger(nameof(ParentOrchestration));
        logger.LogInformation("ParentOrchestration: calling sub-orchestration");
        string result = await ctx.CallSubOrchestratorAsync<string>(nameof(GreetingOrchestration));
        return $"Parent received: {result}";
    }
}

/// <summary>
/// An orchestration that interacts with a durable entity. Demonstrates that
/// entity work items are also filtered.
/// </summary>
public static class CounterOrchestration
{
    [Function(nameof(CounterOrchestration) + "_Start")]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/counter")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(CounterOrchestration));
        return await HttpHelpers.CreateAcceptedResponse(req, instanceId);
    }

    [Function(nameof(CounterOrchestration))]
    public static async Task<int> Run([OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger(nameof(CounterOrchestration));
        var entityId = new EntityInstanceId(nameof(CounterEntity), "sample-counter");

        logger.LogInformation("CounterOrchestration: adding 10 then 20 to counter");
        await ctx.Entities.CallEntityAsync(entityId, "Add", 10);
        await ctx.Entities.CallEntityAsync(entityId, "Add", 20);

        int value = await ctx.Entities.CallEntityAsync<int>(entityId, "Get");
        logger.LogInformation("CounterOrchestration: counter value = {Value}", value);
        return value;
    }
}

// =============================================================================
// Activities
// =============================================================================

/// <summary>
/// A simple activity function. With filtering enabled, only workers that have
/// this activity registered will receive dispatched work items for it.
/// </summary>
public static class SayHello
{
    [Function(nameof(SayHello))]
    public static string Run([ActivityTrigger] string name)
    {
        return $"Hello, {name}!";
    }
}

// =============================================================================
// Entities
// =============================================================================

/// <summary>
/// A simple counter entity. With filtering enabled, only workers that have
/// this entity registered will receive dispatched work items for it.
/// </summary>
public class CounterEntity : TaskEntity<int>
{
    public void Add(int amount) => this.State += amount;
    public void Reset() => this.State = 0;
    public int Get() => this.State;

    [Function(nameof(CounterEntity))]
    public static Task Dispatch([EntityTrigger] TaskEntityDispatcher dispatcher)
        => dispatcher.DispatchAsync<CounterEntity>();
}

// =============================================================================
// Utility functions
// =============================================================================

/// <summary>
/// Generic starter — can schedule any orchestration by name.
/// Useful for testing cross-app filter isolation: schedule an orchestration
/// that this app does NOT have and observe it stays Pending.
/// </summary>
public static class GenericStarter
{
    [Function("StartOrchestration")]
    public static async Task<HttpResponseData> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "start/{name}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string name)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(name);
        return await HttpHelpers.CreateAcceptedResponse(req, instanceId);
    }

    [Function("GetInstanceStatus")]
    public static async Task<HttpResponseData> GetStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instances/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        var meta = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);
        var resp = req.CreateResponse(meta == null ? HttpStatusCode.NotFound : HttpStatusCode.OK);
        resp.Headers.Add("Content-Type", "application/json");
        if (meta == null)
        {
            await resp.WriteStringAsync($"{{\"error\":\"Instance {instanceId} not found\"}}");
        }
        else
        {
            await resp.WriteStringAsync(
                $"{{\"name\":\"{meta.Name}\",\"instanceId\":\"{meta.InstanceId}\"," +
                $"\"status\":\"{meta.RuntimeStatus}\",\"output\":{meta.SerializedOutput ?? "null"}}}");
        }

        return resp;
    }
}

// =============================================================================
// Shared helper
// =============================================================================

internal static class HttpHelpers
{
    internal static async Task<HttpResponseData> CreateAcceptedResponse(HttpRequestData req, string instanceId)
    {
        var resp = req.CreateResponse(HttpStatusCode.Accepted);
        resp.Headers.Add("Content-Type", "application/json");
        await resp.WriteStringAsync($"{{\"instanceId\":\"{instanceId}\"}}");
        return resp;
    }
}
