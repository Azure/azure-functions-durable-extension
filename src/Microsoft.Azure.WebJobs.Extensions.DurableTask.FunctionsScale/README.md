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

