# Worker-owned large payloads for .NET isolated Durable Functions

This optional package externalizes large orchestration and activity payloads in
the .NET isolated worker, before they cross the Functions host boundary. Bound
Durable clients use the same store for starting orchestrations, raising events,
and reading payloads. Installing or upgrading the base Durable Functions worker
extension does not enable this feature or add an Azure Blob dependency.

## Configure once in the worker

Reference `Microsoft.Azure.Functions.Worker.Extensions.DurableTask.AzureBlobPayloads`
and register the store in the Function app's startup:

```csharp
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddDurableTaskExternalizedPayloads(options =>
{
    options.ConnectionString =
        builder.Configuration["PAYLOAD_STORAGE_CONNECTION_STRING"]
        ?? throw new InvalidOperationException("Configure payload storage.");
    options.ContainerName = "durabletask-payloads";
    options.ThresholdBytes = 256 * 1024;
    options.MaxPayloadBytes = 10 * 1024 * 1024;
});
```

Supply production values through Function App application settings or deployment
configuration. Connection strings can use Key Vault references. For managed
identity authentication, configure `AccountUri` and `Credential` instead of
`ConnectionString`, using the existing Durable Task SDK options. All scaled-out
workers must retain access to the backing store across deployments.

An existing or custom SDK `PayloadStore` can also be registered as a singleton,
with `LargePayloadStorageOptions` configured through the options system, followed
by `builder.Services.AddDurableTaskExternalizedPayloads()`. Keep that store open
for the worker process's lifetime. Store configuration is shared by bound clients
and invocation processing in that worker.

This does not start another task worker. The Functions host still owns work-item
polling and completion. These settings are independent of an AzureManaged
provider's `host.json` payload settings; host-side offloading is not required for
SDK-owned references. Other producers and host-generated payloads need their own
size/offload policy.

## Reference and execution behavior

- SDK-owned serialized payloads at or above the threshold are stored out of band. The
  default threshold is 256 KiB and the default maximum stored payload is 10 MiB.
  Size is measured as serialized UTF-8, before compression.
- Both attributed activities and registered `TaskActivity` implementations resolve
  input before user code runs and externalize output before returning to the host.
- Scheduled activities retain the one-argument JSON array required by the host.
  Only the serialized argument is externalized and measured against the size
  limits, not its surrounding array. Array-valued arguments retain both layers.
- With this opt-in, binary activity bindings request serialized string transport
  through worker function metadata. The host forwards the reference without
  decoding it as Base64; the worker resolves it before deserializing `byte[]`.
  Unconfigured bindings retain their existing transport.
- Orchestration input/history is hydrated in a separately decoded invocation
  message. Storage I/O occurs before and after the orchestration runner, not
  inside deterministic replay. Host history keeps the original references.
- Activity inputs recorded in replay history are not downloaded, because replay
  only needs their scheduling metadata. Explicit client history reads hydrate
  payload fields that are present. Fields omitted by the provider or SDK remain
  omitted.
- Outgoing activity/sub-orchestration inputs, orchestration output, external-event
  data and custom status use the same configuration. Null and inline payloads
  preserve their existing behavior.
- References are JSON strings, so host JSON readers preserve them without another
  serialization layer. Recognized raw or JSON-quoted references are not uploaded
  again. A whole payload that matches the store's token syntax is reserved for
  transport; wrap a literal token in an application object to pass it as data.
- Missing payloads, storage failures, invalid configuration, and cancellation are
  surfaced; the worker does not silently fall back to a reference as user data.
  Storage retries are distinct from durable activity retry policies.

## Scope and retention

Host-owned built-in activities, including `CallHttpAsync` (`BuiltIn::HttpActivity`),
do not run in the isolated worker. Their scheduled inputs stay inline and are not
subject to this opt-in's storage threshold or maximum. They remain subject to
their existing host/backend size limits and any separately configured supported
host-side payload policy.

Entity payloads and entity protocol envelopes are not externalized by this opt-in;
their existing behavior is unchanged. Entity LP support requires separate
correlation-aware envelope handling.

Use configured bound clients to read hydrated payloads. Host management HTTP
endpoints and clients without the same configuration can expose references.
Different SDKs exchanging references must agree on their format and storage
access. This package does not enable auto-purge: retain blobs for as long as any
retained history references them, and manage their lifecycle separately.

Recognized references are trusted transport input, not an authorization boundary.
Use least-privilege storage credentials and do not pass untrusted references into
Durable APIs without validating their permitted storage locations.
