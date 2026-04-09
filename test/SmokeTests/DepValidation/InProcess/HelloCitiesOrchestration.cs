// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace InProcessSmokeTest;

/// <summary>
/// A simple function chaining orchestration using the in-process (WebJobs) model
/// to validate the WebJobs extension NuGet package works correctly at runtime.
/// </summary>
public static class HelloCitiesOrchestration
{
    /// <summary>
    /// Returns the version of the loaded WebJobs extension assembly.
    /// </summary>
    [FunctionName("SdkVersionCheck")]
    public static HttpResponseMessage SdkVersionCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req)
    {
        string[] extensionAssemblies = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
        };

        var loadedVersions = new SortedDictionary<string, string>();
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            AssemblyName name = asm.GetName();
            if (extensionAssemblies.Any(
                p => string.Equals(name.Name, p, StringComparison.OrdinalIgnoreCase)))
            {
                string? infoVersion = asm
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                        ?.InformationalVersion;

                    if (infoVersion != null && infoVersion.Contains('+'))
                    {
                        infoVersion = infoVersion[..infoVersion.IndexOf('+')];
                    }

                    loadedVersions[name.Name!] = infoVersion ?? name.Version?.ToString() ?? "unknown";
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Content = new StringContent(
            JsonSerializer.Serialize(loadedVersions),
            Encoding.UTF8,
            "application/json");
        return response;
    }

    [FunctionName(nameof(HelloCitiesOrchestration))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        string result = "";
        result += await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo") + " ";
        result += await context.CallActivityAsync<string>(nameof(SayHello), "London") + " ";
        result += await context.CallActivityAsync<string>(nameof(SayHello), "Seattle");
        return result;
    }

    [FunctionName(nameof(SayHello))]
    public static string SayHello(
        [ActivityTrigger] string cityName,
        ILogger log)
    {
        log.LogInformation("Saying hello to {CityName}!", cityName);
        return $"Hello, {cityName}!";
    }

    [FunctionName("HelloCitiesOrchestration_HttpStart")]
    public static async Task<HttpResponseMessage> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
        string instanceId = await starter.StartNewAsync(nameof(HelloCitiesOrchestration), (object?)null);
        log.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);
        return starter.CreateCheckStatusResponse(req, instanceId);
    }
}
