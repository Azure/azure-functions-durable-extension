// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace DotNetIsolated;


/// <summary>
/// A simple counter, demonstrating entity use.
/// </summary>
public class Counter
{
    public int CurrentValue { get; set; }

    public void Add(int amount)
    {
        this.CurrentValue += amount;
    }

    public void Reset()
    {
        this.CurrentValue = 0;
    }

    public int Get()
    {
        return this.CurrentValue;
    }

    [Function(nameof(Counter))]
    public static Task CounterEntity([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<Counter>();
    }
}

/// <summary>
/// Provides three http triggers to test the counter entity.
/// </summary>
/// <example>
/// (POST) send 5 increment signals to the counter instance @counter@aa:
///   curl http://localhost:7071/api/counter/aa -d '5'
/// (GET) read the current value of the counter instance @counter@aa:
///   curl http://localhost:7071/api/counter/aa
/// (DELETE) delete the counter instance @counter@aa:
///   curl http://localhost:7071/api/counter/aa -X delete
/// </example>
public static class CounterTest
{
    [Function(nameof(SignalCounter))]
    public static async Task<HttpResponseData> SignalCounter(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "counter/{id}")] HttpRequestData request,
       [DurableClient] DurableTaskClient client,
       FunctionContext executionContext,
       CancellationToken cancellation,
       string id)
    {
        ILogger logger = executionContext.GetLogger(nameof(Counter));

        using StreamReader reader = new StreamReader(request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync();
        if (! int.TryParse(body, out var count))
        {
            var httpResponse = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            httpResponse.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            httpResponse.WriteString($"Request body must contain an integer that indicates the number of signals to send.\n");
            return httpResponse;
        };

        var entityId = new EntityInstanceId("Counter", id);
        logger.LogInformation($"Sending {count} increment messages to {entityId}...");

        await Parallel.ForEachAsync(
            Enumerable.Range(0, count), 
            cancellation, 
            (int i, CancellationToken cancellation) =>
            {
                return new ValueTask(client.Entities.SignalEntityAsync(entityId, "add", 1, cancellation:cancellation));
            });

        logger.LogInformation($"Sent {count} increment messages to {entityId}.");
        return request.CreateResponse(System.Net.HttpStatusCode.Accepted);
    }

    [Function(nameof(ReadCounter))]
    public static async Task<HttpResponseData> ReadCounter(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "counter/{id}")] HttpRequestData request,
      [DurableClient] DurableTaskClient client,
      FunctionContext executionContext,
      string id)
    {
        ILogger logger = executionContext.GetLogger(nameof(Counter));
        var entityId = new EntityInstanceId("Counter", id);

        logger.LogInformation($"Reading state of {entityId}...");
        var response = await client.Entities.GetEntityAsync(entityId, includeState: true);
        if (response?.IncludesState ?? false)
        {
            logger.LogInformation("Entity does not exist.");
        }
        else
        {
            logger.LogInformation("Entity state is: {State}", response!.State.Value);
        }

        if (response == null)
        {
            return request.CreateResponse(System.Net.HttpStatusCode.NotFound);
        }
        else
        {
            int currentValue = response.State.ReadAs<Counter>()!.CurrentValue;
            var httpResponse = request.CreateResponse(System.Net.HttpStatusCode.OK);
            httpResponse.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            httpResponse.WriteString($"{currentValue}\n");
            return httpResponse;
        }
    }

    [Function(nameof(DeleteCounter))]
    public static async Task<HttpResponseData> DeleteCounter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "counter/{id}")] HttpRequestData request,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext,
        string id)
    {
        ILogger logger = executionContext.GetLogger(nameof(Counter));
        var entityId = new EntityInstanceId("Counter", id);
        logger.LogInformation($"Deleting {entityId}...");

        // All entities have a "delete" operation built in, so we can just send a signal
        await client.Entities.SignalEntityAsync(entityId, "delete");

        logger.LogInformation($"Sent deletion signal to {entityId}.");
        return request.CreateResponse(System.Net.HttpStatusCode.OK);
    }
}



