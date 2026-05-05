// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace DurableMiddlewareSample;

public static class Functions
{
    [Function(nameof(StartGreeting))]
    public static async Task<HttpResponseData> StartGreeting(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orchestrators/greeting")] HttpRequestData request,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(GreetingOrchestration), "Tokyo");
        HttpResponseData response = request.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { instanceId });
        return response;
    }

    [Function(nameof(GreetingOrchestration))]
    public static async Task<string> GreetingOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        string city)
    {
        string greeting = await context.CallActivityAsync<string>(nameof(SayHello), city);
        return greeting;
    }

    [Function(nameof(SayHello))]
    public static string SayHello([ActivityTrigger] string city)
    {
        return $"Hello, {city}!";
    }
}