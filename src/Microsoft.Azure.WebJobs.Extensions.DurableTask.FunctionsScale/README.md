# Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale

This package provides **event-driven scaling** (KEDA-based) support for Durable Functions running on the Azure Functions host. It supplies the scale monitors and target scalers that the Azure Functions **Scale Controller** uses to make scaling decisions (adding or removing worker instances) based on the load of the durable task backend. By default, Scale Controller v3 uses `ITargetScaler` (target-based scaling) for scaling decisions.

| | |
|---|---|
| **NuGet Package** | `Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale` |
| **Target Framework** | `net8.0` |
| **Assembly Name** | `Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale` |

## Where This Code Runs

This extension runs **inside the Azure Functions Scale Controller process**, which is a separate process from the Functions host and the language worker. The Scale Controller is an Azure-managed component that monitors trigger load and decides how many instances to provision.

The Scale Controller dynamically loads this extension based on metadata emitted by the host-side `WebJobs.Extensions.DurableTask` extension during the "sync triggers" phase. It does **not** run inside the normal Functions host or any user-facing worker process.

## Architectural Overview

### How It Works

The Scale Controller passes `TriggerMetadata` to this package for each durable trigger. The metadata contains host scaling configuration, backend connection info, and identity/credential details.

Based on the `type` in the metadata, the package resolves a backend-specific `ScalabilityProvider` (via `IScalabilityProviderFactory`). Each provider connects to its backend service to collect scaling metrics (e.g., queue lengths, work-item counts) and exposes them via `ITargetScaler` so the Scale Controller can compute the desired instance count.

### Supported Backends

This package supports scaling for all four Durable Functions storage backends:

1. Azure Storage
2. Netherite
3. MSSQL
4. DurableTask Scheduler

## DTS connections in sovereign clouds

The Azure Managed SDK parses the DTS connection string for both the Durable Functions
provider and this scale extension. SDK version 1.10.2 supports independent settings for
the scheduler endpoint, token audience (`ResourceId`), and Microsoft Entra authority
(`AuthorityHost`). For example, set `DURABLE_TASK_SCHEDULER_CONNECTION_STRING` to:

```text
Endpoint=https://<scheduler-endpoint>;TaskHub=<task-hub>;Authentication=DefaultAzure;ResourceId=https://durabletask.azure.us;AuthorityHost=https://login.microsoftonline.us/
```

- `ResourceId` explicitly selects the token audience, including for credentials supplied
  by the Scale Controller. If omitted or empty, the SDK uses
  `https://durabletask.azure.us` when the current process's `REGION_NAME` starts with
  `usgov` or `usdod` (case-insensitive), and `https://durabletask.io` otherwise.
  Set it explicitly when running locally or when the scaling process's region does not
  identify the target cloud. Other clouds require their appropriate resource ID.
  The SDK normalizes surrounding whitespace, trailing slashes, and an existing
  `/.default` suffix before requesting the token scope.
- `AuthorityHost` must be an absolute HTTPS URI. It overrides the authority for
  SDK-created authority-aware credentials (`DefaultAzure`, `Environment`,
  `WorkloadIdentity`, and `InteractiveBrowser`). If omitted or empty, Azure Identity
  retains its `AZURE_AUTHORITY_HOST` setting or Azure Public default. The SDK does not
  infer the authority from `REGION_NAME` or `ResourceId`. Managed identity and
  developer-tool credentials use their platform/tool cloud configuration.
- When trigger metadata supplies `GetAzureManagedTokenCredential`, the scale extension
  preserves that credential instead of the SDK-created one. The Scale Controller must
  configure its credential for the target cloud; `AuthorityHost` does not reconfigure
  externally supplied credentials. `ResourceId` still determines the requested audience.

No separate Durable Functions `host.json` authority or audience setting is needed.
Applications must also use the Azure Managed provider package version 1.10.2 or later;
upgrading the scale extension alone does not upgrade the application's provider.
