using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace MSSQL;

public static class DurableFunctionsOrchestrationCSharp
{
    /// <summary>
    /// Orchestrates a simple fan-out using the SQL Server provider.
    /// </summary>
    [Function(nameof(DurableFunctionsOrchestrationCSharp))]
    public static async Task<List<string>> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(DurableFunctionsOrchestrationCSharp));
        logger.LogInformation("Running SQL Server orchestration sample.");

        var outputs = new List<string>
        {
            await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"),
            await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"),
            await context.CallActivityAsync<string>(nameof(SayHello), "London"),
        };

        return outputs;
    }

    /// <summary>
    /// Activity that returns a greeting for the requested city.
    /// </summary>
    [Function(nameof(SayHello))]
    public static string SayHello([ActivityTrigger] string name, FunctionContext context)
    {
        ILogger logger = context.GetLogger(nameof(SayHello));
        logger.LogInformation("Saying hello to {name}.", name);
        return $"Hello {name}!";
    }

    /// <summary>
    /// HTTP starter that schedules the orchestration and returns management URLs.
    /// </summary>
    [Function("DurableFunctionsHttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext context)
    {
        ILogger logger = context.GetLogger(nameof(DurableFunctionsHttpStart));

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(DurableFunctionsOrchestrationCSharp));
        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
