## Distributed Tracing V2 for Durable Functions

The second version of distributed tracing for Durable Functions is in preview.

#### Benefits
1. All three Durable Functions storage providers are supported: Azure Storage, Netherite, and Microsoft SQL Server.
2. It provides additional information in terms of spans and tags.

![Trace](../images/FunctionChaining.png)

### Enabling Distributed Tracing V2
To use Distributed Tracing V2, all you will need to do is update your app's host.json and add an environment variable for the Application Insights resource.

#### Update host.json
To use Distributed Tracing V2, please update your host.json settings to include the following settings: `DistributedTracingEnabled` and `Version`. The sample app's host.json is already updated with this information.

```
  "durableTask": {
    "tracing": {
      "DistributedTracingEnabled": true,
      "Version": "V2"
    }
  }
```

#### Add Application Insights
You will also need to specify an Application Insights resource in the environment variables. If you haven't done so already, please follow [these instructions](https://learn.microsoft.com/en-us/azure/azure-monitor/app/create-workspace-resource#copy-the-connection-string) to create an Application Insights resource.

Next, you'll need to copy the connection string or instrumentation key for that resource to add in the environment variables. We recommend adding the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable, but you can also add `APPINSIGHTS_INSTRUMENTATIONKEY`. If you are running your app locally, then add this environment variable in `local.settings.json`. If you are running the app in Azure, then add it under `Configuration` as an application setting.

If the Application Insights resource has local authentication disabled, also configure `APPLICATIONINSIGHTS_AUTHENTICATION_STRING`. Use `Authorization=AAD` for the function app's system-assigned managed identity, or `Authorization=AAD;ClientId=<USER_ASSIGNED_CLIENT_ID>` for a user-assigned managed identity. The selected identity needs the `Monitoring Metrics Publisher` role on the Application Insights resource. Microsoft Entra authentication for Application Insights isn't supported by the Functions host during local development.

### Optional Durable telemetry enrichment

An in-process app can explicitly register Application Insights initializers for Durable distributed tracing V2. Host Application Insights registrations are not inherited automatically.

```csharp
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(DistributedTracingSample.Startup))]

namespace DistributedTracingSample
{
    public sealed class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var initializer = new ServiceTelemetryInitializer();

            // The same externally owned instance deliberately enriches both destinations.
            builder.Services.AddSingleton<ITelemetryInitializer>(initializer);
            builder.Services.AddDurableTaskTelemetryInitializer(initializer);
        }
    }

    public sealed class ServiceTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Cloud.RoleName = "orders-functions";
            if (telemetry is ISupportProperties properties)
            {
                properties.Properties["environment"] = "production";
            }
        }
    }
}
```

Existing Durable V2 records now receive `cloud_RoleName=orders-functions` and `environment=production`. No additional records are emitted. A role name identifies the application in Application Insights; it is not an Azure authorization role.

You can instead use the factory overload to resolve an existing singleton-compatible initializer from dependency injection:

```csharp
builder.Services.AddDurableTaskTelemetryInitializer(
    services => services.GetRequiredService<ServiceTelemetryInitializer>());
```

Register the concrete service separately as a singleton before using this form. The factory runs once when Durable V2 tracing starts. It is not resolved for disabled tracing, V1, or None. Durable borrows the result and does not dispose it, so do not return scoped services or create orphaned disposable instances in the factory.

Custom initializers remain trusted, mutable SDK code. They must be thread-safe, tolerate repeated initialization, and work without a live HTTP request or function invocation. Deliberate custom operation names are retained and still receive the existing instance-ID suffix when configured. Do not infer Durable-specific identity from ambient HTTP, logging, or activity context.

**Privacy and process boundaries:** Only initializers explicitly registered with `AddDurableTaskTelemetryInitializer` participate. Host processors, sampling, filtering, redaction, and `TelemetryClient.Context` are not inherited. Review any added properties because host redaction processors will not protect Durable records. Initializers registered only in a .NET isolated worker cannot customize host-process Durable spans.

Initializers are selected once for each host generation. Restart after changing registrations. To roll back, remove the Durable registration and restart; previously ingested telemetry is unchanged. This feature customizes Durable records but does not suppress the separate host function telemetry described in issue #1792.

#### Run the sample
The sample shows how Distributed Tracing V2 improves the observability of some flagship Durable Functions patterns.

- Function Chaining
![Function Chaining](../images/FunctionChaining.png)

- Fan Out Fan In
![Fan Out Fan In](../images/FanOutFanIn.png)

- Async HTTP APIs (currently not shown in traces)

- Monitoring
![Monitoring](../images/Monitoring.png)

- Human Interation
![HumanInteraction1](../images/HumanInteraction1.png)
![HumanInteraction2](../images/HumanInteraction2.png)

- Entities (currently not supported)
