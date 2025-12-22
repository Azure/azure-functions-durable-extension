// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using DurableTask.Core.History;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class GetOrchestrationHistory
{
    public static readonly EntityInstanceId entityId = new(nameof(SimpleEntity), "singleton");

    [Function(nameof(ParentOrchestration))]
    public static async Task<ComplexInput> ParentOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ComplexInput? input = context.GetInput<ComplexInput>();

        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (input.OrchestrationType == "succeed")
        {
            // Try setting various fields to null to ensure serialization of the history works as expected.
            input.Tags = null;
            await context.CallSubOrchestratorAsync(
                nameof(CallLargeOutputTasksSubOrchestration),
                input,
                new SubOrchestrationOptions { InstanceId = input.SubOrchestrationInstanceId }
           );
        }
        else
        {
            // Try setting various fields to null to ensure serialization of the history works as expected.
            input.OrchestrationType = null;
            await context.CallSubOrchestratorAsync(
                nameof(FailSubOrchestration),
                input,
                new SubOrchestrationOptions { InstanceId = input.SubOrchestrationInstanceId, Tags = input.Tags }
           );
        }

        return input;
    }

    [Function(nameof(FailSubOrchestration))]
    public static async Task FailSubOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await context.CallActivityAsync<string>(nameof(ThrowExceptionActivity), new TaskOptions {  Tags = context.GetInput<ComplexInput>()?.Tags });
    }

    [Function(nameof(CallLargeOutputTasksSubOrchestration))]
    public static async Task<ComplexInput> CallLargeOutputTasksSubOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ComplexInput? input = context.GetInput<ComplexInput>();
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        await context.CallActivityAsync<string>(nameof(LargeOutputActivity), input.OutputSize);

        if (input.CallEntities)
        {
            await context.Entities.SignalEntityAsync(entityId, "set", input.OutputSize);
            // Add a timer to give the signal some more time to be processed before we read the entity state.
            // We could make this a "call" rather than a "signal", but this ensures we get more history event types in the orchestration history.
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(5), CancellationToken.None);
            await context.Entities.CallEntityAsync<string>(entityId, "get");
        }
        else
        {
            await context.CallActivityAsync<string>(nameof(LargeOutputActivity), input.OutputSize);
        }
        return input;
    }

    [Function(nameof(LargeOutputActivity))]
    public static string LargeOutputActivity([ActivityTrigger] int outputSize, FunctionContext executionContext)
    {
        return new string('a', outputSize);
    }

    [Function(nameof(ThrowExceptionActivity))]
    public static string ThrowExceptionActivity([ActivityTrigger] FunctionContext executionContext)
    {
        throw new Exception("Failure!");
    }

    [Function(nameof(GetInstanceHistory))]
    public static async Task<HttpResponseData> GetInstanceHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        try
        {
            IList<HistoryEvent> history = await client.GetOrchestrationHistoryAsync(instanceId);
            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);

            // The WriteAsJsonAsync method does not serialize the HistoryEvent polymorphic types correctly, so we use WriteStringAsync instead
            // and use JsonConvert to serialize the history ourselves.
            await response.WriteStringAsync(JsonConvert.SerializeObject(history));
            return response;
        }
        catch (ArgumentException)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }
    }

    [Function(nameof(GetOrchestrationHistory_HttpStart))]
    public static async Task<HttpResponseData> GetOrchestrationHistory_HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string orchestrationType,
        string subOrchestrationInstanceId,
        int outputSize,
        bool callEntities,
        string tagsKey,
        string tagsValue)
    {
        ILogger logger = executionContext.GetLogger(nameof(GetOrchestrationHistory_HttpStart));
        Dictionary<string, string> tags = new() { { tagsKey, tagsValue } };

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ParentOrchestration),
            new ComplexInput(
                orchestrationType,
                subOrchestrationInstanceId,
                outputSize,
                callEntities,
                tags),
            new StartOrchestrationOptions { Tags = tags });

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(SimpleEntity))]
    public static Task SimpleEntity([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync(operation =>
        {
            switch (operation.Name)
            {
                case "get":
                    return new(operation.State.GetState<string>());
                case "set":
                    int size = operation.GetInput<int>();
                    operation.State.SetState(new string('a', size));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown operation '{operation.Name}'");
            }
            return default;
        });
    }

    public class ComplexInput(
        string? orchestrationType,
        string subOrchestrationInstanceId,
        int outputSize,
        bool callEntities,
        Dictionary<string, string>? tags)
    {
        public bool CallEntities { get; set; } = callEntities;

        public string? OrchestrationType { get; set; } = orchestrationType;

        public string SubOrchestrationInstanceId { get; set; } = subOrchestrationInstanceId;

        public int OutputSize { get; set; } = outputSize;

        public Dictionary<string, string>? Tags { get; set; } = tags;

        public override bool Equals(object? obj)
        {
            if (obj is not ComplexInput other)
            {
                return false;
            }
            return other.CallEntities == this.CallEntities
                && ((other.OrchestrationType is null && this.OrchestrationType is null)
                || (other.OrchestrationType is not null && this.OrchestrationType is not null
                && other.OrchestrationType.Equals(this.OrchestrationType)))
                && other.SubOrchestrationInstanceId.Equals(this.SubOrchestrationInstanceId)
                && other.OutputSize == this.OutputSize
                && ((other.Tags is null && this.Tags is null)
                || (other.Tags is not null && this.Tags is not null
                && other.Tags.OrderBy(x => x.Key).SequenceEqual(this.Tags.OrderBy(x => x.Key))));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.CallEntities, this.OrchestrationType, this.SubOrchestrationInstanceId, this.OutputSize, this.Tags);
        }
    }
}
