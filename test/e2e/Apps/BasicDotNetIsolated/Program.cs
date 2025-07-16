// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register a custom service for testing dependency injection in entities
        services.AddSingleton<MyInjectedService>();
    })
    .Build();


// Bool.parse
if (Environment.GetEnvironmentVariable("DURABLE_ATTACH_DEBUGGER") == "True") {
    Debugger.Launch();
}

host.Run();

// This empty class is used to demonstrate dependency injection in entities.
internal class MyInjectedService { }