// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableNetIsolated.Untyped;

/// <remarks>
/// The set of functions in this class demonstrate how to use Durable Functions using untyped orchestration and activities.
/// The programming model used is the most similar to the WebJobs-based Durable Functions experience for .NET in-process.
/// See the <see cref="TypedSample.HelloCitiesTyped"/> implementation for how to use the newer "typed" programming model.
/// </remarks>
internal static class HelloSequenceUntyped
{
    /// <summary>
    /// HTTP-triggered function that starts the <see cref="HelloCitiesUntyped"/> orchestration.
    /// </summary>
    /// <param name="req">The HTTP request that was used to trigger this function.</param>
    /// <param name="client">The DurableTask client that is used to start and manage orchestration instances.</param>
    /// <param name="executionContext">The Azure Functions execution context, which is available to all function types.</param>
    /// <returns>Returns an HTTP response with more information about the started orchestration instance.</returns>
    [Function(nameof(StartHelloCitiesUntyped))]
    public static async Task<HttpResponseData> StartHelloCitiesUntyped(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(StartHelloCitiesUntyped));

        string? providedInstanceId = System.Web.HttpUtility.ParseQueryString(req.Url.Query)["instanceId"];

        StartOrchestrationOptions options;
        string instanceId;

        // If the user provided an instance ID, we will use it to start the orchestration.
        if (!string.IsNullOrEmpty(providedInstanceId))
        {
            options = new StartOrchestrationOptions
            {
                InstanceId = providedInstanceId
            };

            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HelloCitiesUntyped), options);
        }
        // If the user did not provide an instance ID, we will generate a new one.
        else
        {
            instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(HelloCitiesUntyped));

        }

        logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);

        return client.CreateCheckStatusResponse(req, instanceId);
    }

    /// <summary>
    /// Orchestrator function that calls the <see cref="SayHelloUntyped"/> activity function several times consecutively.
    /// </summary>
    /// <param name="requestState">The serialized orchestration state that gets passed to the function.</param>
    /// <returns>Returns an opaque output string with instructions about what actions to persist into the orchestration history.</returns>
    [Function(nameof(HelloCitiesUntyped))]
    public static async Task<string> HelloCitiesUntyped([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string result = "";
        result += await context.CallActivityAsync<string>(nameof(SayHelloUntyped), "Tokyo") + " ";
        result += await context.CallActivityAsync<string>(nameof(SayHelloUntyped), "London") + " ";
        result += await context.CallActivityAsync<string>(nameof(SayHelloUntyped), "Seattle");
        return result;
    }

    /// <summary>
    /// Simple activity function that returns the string "Hello, {input}!".
    /// </summary>
    /// <param name="cityName">The name of the city to greet.</param>
    /// <returns>Returns a greeting string to the orchestrator that called this activity.</returns>
    [Function(nameof(SayHelloUntyped))]
    public static string SayHelloUntyped([ActivityTrigger] string cityName, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(SayHelloUntyped));
        logger.LogInformation("Saying hello to {name}", cityName);
        return $"Hello, {cityName}!";
    }
}
