// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerApplication =>
    {
        // Register async middleware to test the fix for https://github.com/microsoft/durabletask-dotnet/issues/158
        // This middleware performs async operations that previously caused non-determinism exceptions
        workerApplication.UseMiddleware<Microsoft.Azure.Durable.Tests.E2E.TestAsyncMiddleware>();
    })
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register a custom service for testing dependency injection in entities
        services.AddSingleton<MyInjectedService>();

        // Register the custom exception properties provider
        services.AddSingleton<Microsoft.DurableTask.Worker.IExceptionPropertiesProvider, TestExceptionPropertiesProvider>();
    })
    .Build();


// Bool.parse
if (Environment.GetEnvironmentVariable("DURABLE_ATTACH_DEBUGGER") == "True")
{
    Debugger.Launch();
}

host.Run();

// This empty class is used to demonstrate dependency injection in entities.
internal class MyInjectedService { }

// Custom exception properties provider for testing
public class TestExceptionPropertiesProvider : Microsoft.DurableTask.Worker.IExceptionPropertiesProvider // This needs the full path identifier now.
{
    public IDictionary<string, object?>? GetExceptionProperties(Exception exception)
    {
        return exception switch
        {
            ArgumentOutOfRangeException e => new Dictionary<string, object?>
            {
                ["Name"] = e.ParamName ?? string.Empty,
                ["Value"] = e.ActualValue ?? string.Empty,
            },
            Microsoft.Azure.Durable.Tests.E2E.BusinessValidationException e => new Dictionary<string, object?>
            {
                ["StringProperty"] = e.StringProperty,
                ["IntProperty"] = e.IntProperty,
                ["LongProperty"] = e.LongProperty,
                ["DateTimeProperty"] = e.DateTimeProperty,
                ["DictionaryProperty"] = e.DictionaryProperty,
                ["ListProperty"] = e.ListProperty,
                ["NullProperty"] = e.NullProperty,
            },
            _ => null // No custom properties for other exceptions
        };
    }
}