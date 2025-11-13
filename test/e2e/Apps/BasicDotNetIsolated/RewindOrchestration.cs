// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class RewindOrchestration
{
    private static readonly ConcurrentDictionary<string, int> invocationCounts = [];
    private static readonly EntityInstanceId entityId = new(nameof(InvocationCounterEntity), "entity");

    [Function(nameof(RewindParentOrchestration))]
    public static async Task<ConcurrentDictionary<string, int>> RewindParentOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        OrchestrationInput? input = context.GetInput<OrchestrationInput>();
        if (input?.Name == "run")
        {
            await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(10), CancellationToken.None);
            return [];
        }
        else if (input?.Name == "complete")
        {
            return [];
        }
        else if (input?.Name == "fail")
        {
            Task[] subOrchestrationTasks = 
            {
                context.CallSubOrchestratorAsync<string>(
                    nameof(SucceedSubOrchestration), "succeed_sub_1"),
                context.CallSubOrchestratorAsync<string>(
                    nameof(FailParentSubOrchestration), new OrchestrationInput("fail_parent_sub_1", input.NumFailures, input.CallEntities)),
                context.CallSubOrchestratorAsync<string>(
                    nameof(FailParentSubOrchestration), new OrchestrationInput("fail_parent_sub_2", input.NumFailures, input.CallEntities)),
                context.CallSubOrchestratorAsync<string>(
                    nameof(SucceedSubOrchestration), "succeed_sub_2")
            };
            await Task.WhenAll(subOrchestrationTasks);
            return invocationCounts;
        }
        else
        {
            throw new ArgumentException("Invalid input");
        }
    }

    [Function(nameof(FailChildSubOrchestration))]
    public static async Task<string> FailChildSubOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        OrchestrationInput input = context.GetInput<OrchestrationInput>()!;
        List<Task> tasks =
        [
            context.CallActivityAsync<string>(nameof(SucceedActivity), input.Name + "_succeed_activity"),
            context.CallActivityAsync<string>(nameof(FailActivity), new OrchestrationInput(input.Name + "_fail_activity_1", input.NumFailures, input.CallEntities)),
            context.CallActivityAsync<string>(nameof(FailActivity), new OrchestrationInput(input.Name + "_fail_activity_2", input.NumFailures, input.CallEntities))
        ];
        if (input.CallEntities)
        {
            tasks.Add(context.Entities.SignalEntityAsync(entityId, input.Name + "_signal_entity"));
            tasks.Add(context.Entities.CallEntityAsync(entityId, input.Name + "_call_entity"));
        }
        await Task.WhenAll(tasks);
        return "Ok, sub done!";
    }

    [Function(nameof(FailParentSubOrchestration))]
    public static async Task<string> FailParentSubOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        OrchestrationInput input = context.GetInput<OrchestrationInput>()!;
        await context.CallActivityAsync<string>(nameof(SucceedActivity), input.Name + "_succeed_activity");
        if (input.CallEntities)
        {
            await context.Entities.CallEntityAsync(entityId, input.Name + "_call_entity");
        }
        await context.CallSubOrchestratorAsync<string>(nameof(FailChildSubOrchestration), new OrchestrationInput(input.Name + "_child", input.NumFailures, input.CallEntities));
        return "Ok, sub done!";
    }

    [Function(nameof(SucceedSubOrchestration))]
    public static async Task<string> SucceedSubOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await context.CallActivityAsync<string>(nameof(SucceedActivity), context.GetInput<string>() + "_succeed_activity");
        return "Ok, sub done!";
    }

    [Function(nameof(SucceedActivity))]
    public static string SucceedActivity([ActivityTrigger] string input, FunctionContext executionContext)
    {
        UpdateInvocationCount(input);
        return $"Hello, {input}!";
    }

    [Function(nameof(FailActivity))]
    public static string FailActivity([ActivityTrigger] OrchestrationInput failInfo, FunctionContext executionContext)
    {
        if (UpdateInvocationCount(failInfo.Name!) <= failInfo.NumFailures)
        {
            throw new Exception("Failure!");
        }
        return "Success!";
    }

    [Function(nameof(RewindInstance))]
    public static async Task<HttpResponseData> RewindInstance(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        string rewindReason = "Rewinding the instance for testing.";
        try
        {
            await client.RewindInstanceAsync(instanceId, rewindReason);

        }
        catch (InvalidOperationException)
        {
            return req.CreateResponse(HttpStatusCode.PreconditionFailed);
        }
        catch (ArgumentException)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
        catch (NotImplementedException)
        {
            return req.CreateResponse(HttpStatusCode.NotImplemented);
        }
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(HttpStart_RewindOrchestration))]
        public static async Task<HttpResponseData> HttpStart_RewindOrchestration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext,
            string input,
            int numFailures,
            bool callEntities,
            bool? delay)
    {
        invocationCounts.Clear();
        ILogger logger = executionContext.GetLogger(nameof(HttpStart_RewindOrchestration));

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RewindParentOrchestration),
            new OrchestrationInput(input, numFailures, callEntities),
            delay == true ? new StartOrchestrationOptions { StartAt = DateTimeOffset.UtcNow.AddMinutes(1) } : null); 

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(InvocationCounterEntity))]
    public static Task InvocationCounterEntity([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync(operation =>
        {
            UpdateInvocationCount(operation.Name);
            return default;
        });
    }

    private static int UpdateInvocationCount(string key)
    {
        if (!invocationCounts.TryGetValue(key, out int invocationCount))
        {
            invocationCount = 0;
            invocationCounts[key] = invocationCount;
        }
        invocationCounts[key] = ++invocationCount;
        return invocationCount;
    }

    public class OrchestrationInput(string name, int numFailures, bool callEntities)
    {
        public string? Name { get; set; } = name;

        public int NumFailures { get; set; } = numFailures;

        public bool CallEntities { get; set; } = callEntities;
    }
}
