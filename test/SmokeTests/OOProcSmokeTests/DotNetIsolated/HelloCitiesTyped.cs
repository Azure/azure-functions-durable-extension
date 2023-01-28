// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.DurableTask;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.DurableTask.Client;

namespace DotNetIsolated.Typed;

public static class HelloCitiesTypedStarter
{
    /// <summary>
    /// HTTP-triggered function that starts the <see cref="HelloCitiesTyped"/> orchestration.
    /// </summary>
    /// <param name="req">The HTTP request that was used to trigger this function.</param>
    /// <param name="client">The DurableTask client that is used to start and manage orchestration instances.</param>
    /// <param name="executionContext">The Azure Functions execution context, which is available to all function types.</param>
    /// <returns>Returns an HTTP response with more information about the started orchestration instance.</returns>
    [Function(nameof(StartHelloCitiesTyped))]
    public static async Task<HttpResponseData> StartHelloCitiesTyped(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(StartHelloCitiesTyped));

        // Source generators are used to generate type-safe extension methods for scheduling class-based
        // orchestrators that are defined in the current project. The name of the generated extension methods
        // are based on the names of the orchestrator classes. Note that the source generator will *not*
        // generate type-safe extension methods for non-class-based orchestrator functions.
        string instanceId = await client.ScheduleNewHelloCitiesTypedInstanceAsync();
        logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);

        return client.CreateCheckStatusResponse(req, instanceId);
    }
}

/// <summary>
/// Class-based orchestrator function implementation. Source generators are used
/// to generate a singleton, static instance of this class and also a function definitions
/// that invokes the <see cref="OnRunAsync"/> method.
/// </summary>
[DurableTask(nameof(HelloCitiesTyped))]
public class HelloCitiesTyped : TaskOrchestrator<string?, string>
{
    public async override Task<string> RunAsync(TaskOrchestrationContext context, string? input)
    {
        // Source generators are used to generate the type-safe activity function
        // call extension methods on the context object. The names of these generated
        // methods are derived from the names of the activity classes. Note that both
        // activity classes and activity functions are supported by the source generator.
        string result = "";
        result += await context.CallSayHelloTypedAsync("Tokyo") + " ";
        result += await context.CallSayHelloTypedAsync("London") + " ";
        result += await context.CallSayHelloTypedAsync("Seattle");
        return result;
    }
}

/// <summary>
/// Class-based activity function implementation. Source generators are used to a generate an activity function
/// definition that creates an instance of this class and invokes its <see cref="OnRun"/> method.
/// </summary>
[DurableTask(nameof(SayHelloTyped))]
public class SayHelloTyped : TaskActivity<string, string>
{
    private readonly ILogger? logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SayHelloTyped"/> class.
    /// This class is initialized once for every activity execution.
    /// </summary>
    /// <remarks>
    /// Activity class constructors support constructor-based dependency injection.
    /// The injected services are provided by the function's <see cref="FunctionContext.InstanceServices"/> property.
    /// </remarks>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> injected by the Azure Functions runtime.</param>
    public SayHelloTyped(ILoggerFactory? loggerFactory)
    {
        this.logger = loggerFactory?.CreateLogger<SayHelloTyped>();
    }

    public override Task<string> RunAsync(TaskActivityContext context, string cityName)
    {
        this.logger?.LogInformation("Saying hello to {name}", cityName);
        return Task.FromResult($"Hello, {cityName}!");
    }
}
