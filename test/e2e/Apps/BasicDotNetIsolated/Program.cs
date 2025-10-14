// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.DurableTask.Worker;
using System.Diagnostics;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register a custom service for testing dependency injection in entities
        services.AddSingleton<MyInjectedService>();
        
        // Register the custom exception properties provider
        services.AddSingleton<IExceptionPropertiesProvider, TestExceptionPropertiesProvider>();
    })
    .Build();


// Bool.parse
if (Environment.GetEnvironmentVariable("DURABLE_ATTACH_DEBUGGER") == "True") {
    Debugger.Launch();
}

host.Run();

// This empty class is used to demonstrate dependency injection in entities.
internal class MyInjectedService { }

// Custom exception properties provider for testing
public class TestExceptionPropertiesProvider : IExceptionPropertiesProvider
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