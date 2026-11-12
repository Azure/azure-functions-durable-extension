# Durable activity retry metadata

The Functions extension forwards durable retry information produced by DTFx.
It does not calculate attempts, own retry backoff, or expose a new .NET activity
context API. JavaScript consumes this transport through its optional
`context.activityRetryContext` API. Functions host-managed `context.retryContext`
is separate and remains unchanged.

## Activity invocation contract

Both the legacy activity shim and middleware-passthrough activity execution copy
scheduled `DurableTask.Core.RetryMetadata` into the internal activity context.
The activity trigger exports `durableActivityRetryMetadata` as a **JSON string**
in binding data, which the Functions host forwards in
`InvocationRequest.triggerMetadata`:

```json
{"AttemptNumber":2,"MaxAttempts":3,"RetryGroupId":"retry-group-value"}
```

These PascalCase names are an internal transport contract, independent of
customer input serializer settings. The `@azure/functions` Node programming model
JSON-decodes string trigger metadata and recursively camel-cases the parsed object before
exposing `InvocationContext.triggerMetadata`. The JavaScript SDK validates that
native camelCase representation (or the original wire string when supplied
directly) without substituting defaults before exposing its customer-facing
properties. This normalization does not change the extension's wire-level JSON
string contract.

Present metadata must have positive `int32` attempt and maximum counts, an
attempt no greater than the maximum, and a non-whitespace group ID. Invalid
metadata raises a contract error before invoking customer code. Missing metadata
is omitted, not converted to attempt 1. This preserves calls without a retry
policy, older components/history, and direct admin activity invocations.

The group ID identifies a logical activity call and is stable across its attempts
and replay. It is not an idempotency key. Redelivery of an attempt does not advance
its attempt number. The maximum includes the initial attempt, but retry timeout
or a non-retriable failure can stop the policy earlier.

## History

Legacy orchestration JSON and protobuf orchestration-trigger history preserve
`RetryMetadata` on scheduled activity events. Protobuf history streaming uses
the same conversion. Completion/failure events continue to link through their
existing scheduled task IDs and do not echo metadata in the durable protocol.

The status/history projection copies scheduled metadata onto its matching
`TaskCompleted` or `TaskFailed` entry before removing the scheduled entry. An
outstanding `TaskScheduled` entry retains its metadata. Existing outer response
shape, input/output visibility flags, and removal of `TaskScheduledId` from
compacted outcomes remain unchanged. For example:

```json
{
  "EventType": "TaskFailed",
  "FunctionName": "processOrder",
  "RetryMetadata": {
    "AttemptNumber": 2,
    "MaxAttempts": 3,
    "RetryGroupId": "retry-group-value"
  }
}
```

Attempt state is derived from existing events: no outcome means pending,
`TaskFailed` means failed, and `TaskCompleted` means completed. Pending includes
both queued and running activities. No new history API, state database,
backoff-state property, or sub-orchestration metadata is added.

Retry tracing is emitted by DTFx through the existing telemetry pipeline. The
extension and JavaScript SDK do not duplicate retry events or span tags.

## Local dependency validation

These uncommitted changes require the matching source-built DTFx Core,
AzureStorage, and ApplicationInsights packages pinned in `Directory.Packages.props`.
The ApplicationInsights package supplies the correlated retry event export.
They are local-only
prereleases, not versions available from the public feed. Their patch versions
must sort above the published stable minimum required by transitive dependencies;
for example, `3.11.3-activityretry.local...` satisfies an existing `>= 3.11.2`
Core requirement, whereas `3.11.2-activityretry.local...` does not. Do not suppress
package downgrade errors or replace the typed API with reflection.

Point `localFeed` at the coordinated runtime worktree's `artifacts\packages`
directory and generate a temporary config outside the repository. The helper
keeps unrelated packages on the repository's existing `upstream-public` feed:

```powershell
$localFeed = 'C:\path\to\durabletask\artifacts\packages'
$localConfig = Join-Path $env:TEMP "activity-retry-$([guid]::NewGuid()).nuget.config"
.\eng\cfs\New-LocalNuGetConfig.ps1 `
    -DestinationPath $localConfig `
    -LocalSourcePath $localFeed `
    -PackagePatterns @('Microsoft.Azure.DurableTask.Core', 'Microsoft.Azure.DurableTask.AzureStorage', 'Microsoft.Azure.DurableTask.ApplicationInsights')

dotnet restore test\FunctionsV2\WebJobs.Extensions.DurableTask.Tests.V2.csproj --configfile $localConfig
dotnet test test\FunctionsV2\WebJobs.Extensions.DurableTask.Tests.V2.csproj --no-restore --configuration Release --filter "FullyQualifiedName~TriggerBindingProviderTests|FullyQualifiedName~ProtobufUtilsTests|FullyQualifiedName~DurableClientHistoryTests|FullyQualifiedName~OutOfProcMiddlewareTests|FullyQualifiedName~ExternalActivitySchedulingTests|FullyQualifiedName~DisabledFunctionDispatchTests"
```

The test command covers both supported target frameworks. No Azure resource or
deployment is required. Keep the local dependency pins and protocol provenance
local until the corresponding runtime and canonical protocol changes are
available upstream.

For DTS provider integration, the existing `Microsoft.DurableTask.AzureManagedBackend`
pin also points to its matching local prerelease. Add the coordinated DTS package
directory as a second source in the generated config:

```powershell
dotnet nuget add source 'C:\path\to\dts\packages' --name dts-local --configfile $localConfig
```

Add these exact-ID mappings inside that config's `packageSourceMapping` element
so the provider and its two matching transitive siblings come from the local feed:

```xml
<packageSource key="dts-local">
  <package pattern="Microsoft.DurableTask.AzureManagedBackend" />
  <package pattern="Microsoft.DurableTask.AzureManagedBackend.Protobuf" />
  <package pattern="Microsoft.DurableTask.Extensions.AzureBlobPayloads.AzureManaged" />
</packageSource>
```

Build the existing FunctionsScale provider consumer and the host extension:

```powershell
dotnet restore src\Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale\Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.csproj --configfile $localConfig
dotnet build src\Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale\Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.csproj --no-restore --configuration Release --version-suffix activityretry.local.20261110.3 --property:IntermediateOutputPath=obj\r3\ --property:OutputPath=bin\r3\
dotnet build src\WebJobs.Extensions.DurableTask\WebJobs.Extensions.DurableTask.csproj --no-restore --configuration Release --version-suffix activityretry.local.20261110.3
```

The shorter FunctionsScale output paths avoid Windows path-length failures in
deep worktree directories. The explicit prerelease suffix is required when the
build automatically packs an extension with prerelease dependencies; it avoids
creating a stable package with prerelease dependencies. These commands produce
local packages only, not a deployment or an end-to-end Functions/DTS execution.
