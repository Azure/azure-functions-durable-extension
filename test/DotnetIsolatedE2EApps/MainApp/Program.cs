using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

// Bool.parse
if (Environment.GetEnvironmentVariable("DURABLE_ATTACH_DEBUGGER") == "True") {
    Debugger.Launch();
}

host.Run();
