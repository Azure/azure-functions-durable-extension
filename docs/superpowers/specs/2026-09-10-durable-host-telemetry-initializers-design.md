# Opt-in host initializer enrichment for Durable distributed tracing V2

**Status:** Approved design, implemented on this branch. Public documentation approval and real Functions-host/Azure staging validation remain merge/release gates.

**Issue:** [#1792 - Duplicate App Insight Telemetry](https://github.com/Azure/azure-functions-durable-extension/issues/1792).

**Source baseline:** Extension commit `22264d83f911c776d06363fdb79e0f864d27b40d`, including the host-configuration constructor and channel forwarding introduced by [#3512](https://github.com/Azure/azure-functions-durable-extension/pull/3512). This spec targets that source baseline, not a promised package release or a 2.x backport. "V2" below means the distributed tracing feature, not the extension package's major version.

**Approved decisions:** V2-only opt-in, default off; retain Durable's private pipeline; import selected host initializers in order; exclude known invocation/HTTP-context initializers; run the existing Durable naming initializer last; retain the normal trusted custom-initializer contract rather than a field-restoring wrapper.

## 1. Problem and intended outcome

Application Insights uses `cloud_RoleName` to identify the application/service that produced a record. A host-registered `ITelemetryInitializer` can set this role and add properties such as `environment` or `serviceOwner`.

Today, `TelemetryActivator.SetupTelemetryConfiguration` creates a private `TelemetryConfiguration`. It adds Durable's naming initializer, but not the host's configured initializers. Both V2 telemetry modules use that private configuration. Even when Entra authentication forwards records through the host channel, forwarding calls `Send` on the channel; it does not run host initializers or processors. [E1]

As a result, a host SQL dependency can have a role and custom properties that are absent from the correlated Durable activity request. A role-filtered dashboard can omit the Durable record. Empty role metadata alone does not imply broken trace correlation.

The change should let an app explicitly opt in to host environment and custom enrichment on existing Durable V2 records. It must not introduce another exporter, another tracking call, or another copy of a record.

### Before and after

Assume an eligible host initializer sets role `orders-functions`, `environment=production`, and `serviceOwner=payments-team`. The following describes ordinary enrichment initializers, not arbitrary user code that deliberately rewrites structural fields.

| Surface | Before / option off | Option on |
| --- | --- | --- |
| Durable activity/orchestration request role | Can be empty | `orders-functions` |
| Durable request custom properties | Host initializer properties missing | Host initializer properties present |
| Durable scheduling dependency role/properties | Same enrichment gap | Same selected enrichment as requests |
| Existing host SQL dependency | Existing host enrichment | Unchanged by this integration |
| Default Durable operation name | Existing Durable name | Existing Durable name unless deliberately customized |
| Span ID, trace ID, parent ID, timing, result | Existing producer values | No integration-driven rewrite |
| Emission count | Existing records | Same records, enriched |
| Role/instance metadata without any applicable source | May be absent | No fabricated role or instance identity |

This addresses the enrichment portion of #1792. It does not establish that every historical duplication scenario is resolved, and implementing it is not by itself a reason to close that issue.

## 2. Validation and its limits

### Runtime evidence

A disposable local .NET 8 probe used real `AddApplicationInsightsWebJobs` registrations and in-memory channels. It used Application Insights SDK **2.23.0** (`2faa7e8b`), WebJobs AI **3.0.45** (`b2c42816`), and DurableTask AI **0.11.0** (`8b906945`). The existing Durable naming initializer, schema/constants, and WebJobs telemetry module were compiled directly from the source baseline, without modifying them.

The probe did not depend on a locally cached extension package being identical to a published release. No telemetry was sent to Azure.

| Experiment | Observed result |
| --- | --- |
| Existing private configuration | `orchestration:Order (order-123)`, empty role, `Success=false` |
| Copy all host initializers under deliberately unrelated activity/scope/HTTP context | Request name became `UnrelatedHostInvocation`, failure changed to `Success=true`, unrelated invocation ID and HTTP client IP were attached |
| Exclude the invocation initializer and conditional HTTP-context initializer; avoid an extra exact SDK correlation initializer | Durable name, failure, span ID, trace ID, parent ID, and duration retained; role and both custom enrichers' properties present |
| Two distinct custom initializer instances of the same type | Both ran, in host order |
| One initializer throws between two enrichers | SDK continued to the later initializer; one request reached the channel |
| Custom operation name before Durable naming | `custom-order` became `custom-order (order-123)` with the existing instance-ID option |
| Host environment initializer | Site plus non-production slot produced `spec-probe-orders-staging`; explicit `WEBSITE_CLOUD_ROLENAME` produced `explicit-role` |
| Both actual V2 modules listening | One enriched `activity:Charge` request and one enriched `entity:Counter:add` request; no extra record for either input span |

The unrelated-context case is an adversarial compatibility probe, not a claim that every production completion has unrelated ambient context.

### Source evidence

The WebJobs invocation initializer reads ambient logger scope and `Activity.Current`. It can overwrite request/operation names and success. The environment initializer instead reads site/slot/role environment variables. [E2]

The Functions host also registers `ScriptTelemetryInitializer`, which is absent from a bare WebJobs host. At inspected Functions host release `v4.1054.200`, it only stamps captured `HostInstanceId` metadata and must be retained. The similarly named isolated-worker initializer is a different process boundary. [E3]

The AI SDK invokes initializers in collection order, handles initializer exceptions individually, then uses the current configuration's processor chain. Its public initializer collection supports snapshot enumeration. Initializers can run again if callers initialize or track an item again. [E4]

### Not yet established

The original probes validate the selection and ordering mechanism. The implementation adds option/SDK compatibility coverage and real WebJobs Application Insights integration tests with raw host and Durable capture, successful and failing activities, replay, entities, and both private-channel and Entra-forwarding paths. These local tests do not establish Functions Script-host restart/load-context behavior, authenticated Azure ingestion, all host versions, or portal presentation.

Full Functions-host initializer inventory/restart coverage and Azure staging remain release requirements in section 9. The historical production cause of #3053's missing spans was not established; this spec does not claim otherwise.

## 3. Scope and alternatives

**Chosen:** Filtered reuse of the completed host configuration's initializer instances, on Durable's existing private configuration.

| Alternative | Decision |
| --- | --- |
| Populate only a role name in Durable | Insufficient: leaves custom initializers and properties disconnected |
| Copy every host initializer | Rejected: the invocation and HTTP-context incompatibilities are demonstrated above |
| Filtered host initializer snapshot | Chosen: narrow integration, ordinary SDK execution and error handling, no new emitter |
| Wrap initializers and restore protected fields | Not chosen: changes the initializer contract, requires a larger field/exception policy, and cannot undo side effects or incorrectly derived custom properties |
| Adopt the whole host telemetry configuration / change activation to `ITelemetryModule` | Out of scope: changes processors and lifecycle, repeating the broader shape of #3009 that #3053 reverted [E5] |

Non-goals: worker-to-host initializer propagation; V1 behavior changes; OpenTelemetry integration; host processor/filter/redaction/sampling parity; a new authentication mechanism; replay suppression; general sanitization or field immutability against arbitrary custom code.

## 4. Configuration contract

Add one public Boolean property to `TraceOptions`:

`UseHostTelemetryInitializers`, default `false`.

`host.json` setting (unreleased):

```json
{
  "version": "2.0",
  "extensions": {
    "durableTask": {
      "tracing": {
        "distributedTracingEnabled": true,
        "version": "V2",
        "useHostTelemetryInitializers": true
      }
    }
  }
}
```

This setting does not exist at the source baseline. It applies only when distributed tracing is enabled and its version is exactly `V2`. It applies to both V2 modules and is independent of whether Entra authentication is configured.

| Configuration | Required behavior |
| --- | --- |
| Setting absent or false | Preserve current initializer order, telemetry pipeline, and startup behavior |
| Setting true, tracing enabled, V2, host configuration available | Install selected host initializers before enabling either listener |
| Setting true, tracing enabled, V2, no host configuration | Keep existing Durable-only tracing; emit one startup warning explaining that requested host enrichment could not be applied |
| Setting true, tracing disabled or version V1/None | Do not import initializers or enable tracing; emit one startup warning explaining the V2/enabled requirement |
| Host configuration available but no eligible entries | Continue with existing Durable initializers; report zero imported entries in the opt-in startup informational event |

Existing configuration binding is sufficient; do not add another configuration source. Log the requested option in the normal options formatter. Extend the existing trace debug string with the new option only; do not refactor unrelated option reporting. Reporting this new key as false in configuration diagnostics is the only intended default-off reporting change.

## 5. Integration and initializer selection

### 5.1 Startup boundary and ownership

Use `TelemetryActivator`'s existing host-aware constructor. Consume the injected host `TelemetryConfiguration` only after its factory has returned. During `SetupTelemetryConfiguration`, enumerate its public `TelemetryInitializers` collection into a local startup snapshot.

Do not independently resolve `IEnumerable<ITelemetryInitializer>`, build another service provider, use `TelemetryConfiguration.Active`, access private members by reflection, or register Durable as another host `ITelemetryModule`. WebJobs finishes additional configuration after initializing modules; changing to a module callback would be the wrong snapshot boundary. [E4]

Borrow the same initializer instances so constructor dependencies and configuration remain intact. Do not clone them, call module initialization on them, mutate the host list, or dispose them. Do not cache them statically or share them across host generations.

Freeze the private initializer list before either V2 listener starts. Later additions/removals in the host list are not synchronized; restart the host to refresh the selection. This freezes membership, not initializer internals: an environment initializer can still read changed environment values.

### 5.2 Exact selection rules

Preserve the relative order and registration multiplicity of eligible host entries. Two instances of the same custom type both run. A deliberately repeated custom instance remains repeated; do not silently change host registration semantics through general reference/type deduplication.

| Host entry | Treatment |
| --- | --- |
| Exact `Microsoft.Azure.WebJobs.Logging.ApplicationInsights.WebJobsTelemetryInitializer` | Exclude; ambient invocation state can overwrite Durable request meaning |
| Exact `Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers.ClientIpHeaderTelemetryInitializer` | Exclude; an ambient HTTP request is not an authoritative client-IP source for a background Durable span |
| Exact SDK `OperationCorrelationTelemetryInitializer` | Do not add another when the private configuration already contains that exact SDK default; keep the existing private instance and position |
| `WebJobsRoleEnvironmentTelemetryInitializer` | Retain; inherits site/slot and explicit role override behavior |
| Host `ScriptTelemetryInitializer` | Retain; host-instance enrichment, not invocation enrichment |
| `HttpDependenciesParsingTelemetryInitializer` | Retain; the inspected implementation only parses HTTP dependencies, not current DurableTask/InProc dependencies |
| `MetricSdkVersionTelemetryInitializer` | Retain; metric-only behavior |
| Custom/unrecognized initializers | Retain under the opt-in trusted-code contract |

For the internal WebJobs type, match its exact full name and its defining assembly identity, using the public `ApplicationInsightsLoggerOptions` assembly as the marker. For the public SDK types, use exact runtime type equality, not `is` checks that also exclude custom subclasses. Do not exclude an entire namespace/assembly, or names containing "Functions" or "Telemetry".

This narrowly depends on an internal WebJobs type name. Add a version-pinned characterization test that obtains the real host registration and proves the unsafe initializer is recognized. A host/SDK upgrade that changes the inventory requires compatibility review; absence of a known type is not proof that its replacement is safe.

### 5.3 Execution order

```text
V2 module constructs RequestTelemetry/DependencyTelemetry from its Activity
  -> existing private SDK defaults
  -> selected host initializer entries, in host order
  -> existing DurableTaskInstanceIdTelemetryInitializer
  -> existing private processors
  -> existing channel (including host-channel forwarding when already configured)
```

Install host entries immediately before the existing Durable naming initializer. Do not change either module's `Track` call or call host initializers manually. The SDK runs them during normal initialization.

Retain the naming initializer's current implementation: a nonempty custom operation name is kept; a missing one falls back to the telemetry name; the existing instance-ID option and exclusion rules are applied afterward. Do not derive a different invocation identity from the host scope. Preserve the existing behavior for missing activities, entity names, and create-orchestration spans.

No new public initializer interface, wrapper, processor, telemetry client, or telemetry module is required. A small private selection/setup helper in `TelemetryActivator` is sufficient.

## 6. Metadata, compatibility, and error semantics

### Supported enrichment contract

Custom initializers remain trusted `ITelemetryInitializer` implementations. They may deliberately customize properties, role names, or operation names. The integration does not restore or reject their mutations.

Consequently, "preserves correlation/result/timing" means the integration and retained built-ins do not rewrite producer values under supported enrichment usage. It is not a guarantee against a custom initializer that sets IDs, success, sampling fields, routing keys, or calls `Track` recursively.

Document that compatible initializers must be thread-safe, tolerate repeated initialization, and handle request/dependency telemetry emitted outside a live function invocation. They should derive Durable-specific metadata from the record's properties, not assume the current HTTP context, logger scope, or `Activity.Current` represents its original execution.

Do not manufacture an invocation ID when no authoritative value exists. An initializer that depends on values supplied by an excluded initializer may need adjustment. Do not call the excluded initializer merely to satisfy such a dependency.

### Precedence and missing fields

Existing SDK/client defaults run first. Eligible host initializers retain their own assignment rules and relative precedence. A later custom setter can override an earlier role; an earlier custom value is not guaranteed to beat a later explicit environment override.

The final Durable naming initializer supplies missing operation names and applies the existing instance-ID option; it is not a general mutation guard. A host custom operation name such as `custom-order` is therefore retained, with an optional ` (order-123)` suffix.

Only initializers are reused. Properties set solely on the host `TelemetryClient.Context` are not copied. Role-name enrichment does not promise identical `cloud_RoleInstance`, invocation/category metadata, client IP, or host SDK-version stamping: some of those are supplied by the deliberately excluded WebJobs initializer. Existing private SDK fallbacks and eligible custom setters still apply.

### Failures and lifecycle

Register selected initializers directly with the private SDK configuration. AI SDK 2.23.0 reports an initializer exception through its diagnostics and continues to subsequent initializers. Preserve that behavior; do not add a broad Durable catch, suppress diagnostics, manually rerun the initializer, or roll back partial mutations.

Emit one opt-in startup informational event with imported/excluded entry counts. Use the existing extension warning mechanism for the configuration mismatches in section 4. Do not log telemetry contents, credentials, or per-item warnings. Do not create new warning/success logs when the option is off.

Stop/dispose the existing listeners before the originating host and its initializer dependencies are torn down. Keep current module flushing and channel ownership. Do not let disposing the Durable side dispose the host channel or borrowed initializer instances.

### Pipeline and privacy boundary

Do not copy host processors, modules, sinks, sampling settings, filters, credentials, or telemetry client context. The existing private pipeline and Entra forwarding/fallback behavior remain unchanged.

This also means custom host redaction/filtering processors do not protect newly enriched Durable records. Users enabling enrichment must review the data their initializers add and must not assume HTTP-local tenant/user data is appropriate to attach to background spans.

Only the host configuration available in this process participates. Custom initializers registered exclusively in a .NET isolated worker remain outside this feature.

## 7. Implementation touchpoints

Paths are repository-relative. The implementation follows these touchpoints; new selection/SDK compatibility and naming-order coverage is grouped in `HostTelemetryInitializerTests.cs`.

| File/surface | Required work |
| --- | --- |
| `src\WebJobs.Extensions.DurableTask\Options\TraceOptions.cs` | Add the opt-in property and precise XML documentation; include it in trace debug output |
| `src\WebJobs.Extensions.DurableTask\Correlation\TelemetryActivator.cs` | Add activation warnings, snapshot/selection helper, imported-count log, and insertion before existing naming initialization; use existing injected host configuration |
| `src\WebJobs.Extensions.DurableTask\Correlation\HostForwardingTelemetryChannel.cs` | Update its isolation remarks to distinguish opt-in initializer reuse from unchanged processor/module isolation; no forwarding behavior change |
| `src\WebJobs.Extensions.DurableTask\Correlation\DurableTaskInstanceIdTelemetryInitializer.cs` | Reuse as-is; cover the new ordering without broadening its behavior |
| `DurableTaskJobHostConfigurationExtensions` and both V2 modules | No production registration, lifecycle, or emission changes expected |
| `test\FunctionsV2\Helpers\TelemetryActivatorTests.cs` | Option gating, selection, full SDK initialization, warnings, ownership and compatibility cases |
| `test\FunctionsV2\Helpers\DurableTaskInstanceIdTelemetryInitializerTests.cs` | Ordering and operation-name/instance-ID precedence cases |
| `test\FunctionsV2\Tests\Unit\DurableTaskOptionsFormatterTests.cs` | Assert new property appears with default false and explicit true |
| `test\FunctionsV2\Tests\E2E\CorrelationEndToEndTests.cs` and existing host helpers | Add actual host AI registration/capture and option binding coverage; capture raw host and Durable emissions |
| `samples\distributed-tracing\v2\DistributedTracingSample\README.md` | Explain opt-in, exclusions, host/worker boundary, custom-enricher example and rollback |
| Public diagnostics/host.json documentation, `pending_docs.md`, `release_notes.md` | Track companion documentation and release availability when implementation is ready; do not advertise an existing release |

Document the public property in `TraceOptions` and the companion user documentation. The baseline WebJobs extension project does not configure generation of its older checked-in XML API file; leave that file and its unrelated historical omissions out of this change. No new package dependency or SDK version bump is expected.

## 8. Rollout

Ship the option defaulting to false. Enable it in a staging app after reviewing the eligible custom initializers, then compare raw emitted records and role-filtered dashboards with the option off/on.

Rollback is setting `useHostTelemetryInitializers` to false and restarting the host. It restores prior enrichment behavior; it does not remove or rewrite previously ingested records.

Record the extension package, Functions host, and AI SDK versions in release evidence. A successful bare-WebJobs probe does not certify every Functions host load context. Do not backport to extension 2.x automatically.

## 9. Acceptance criteria

Use the repository's existing xUnit project/runners; do not introduce a new test framework. Every assertion about counts must inspect raw captured telemetry before name filtering, correlation sorting, or grouping.

### Unit and SDK integration

| Case | Required assertion |
| --- | --- |
| Option off and missing | Existing configuration/order and output unchanged, including V1; no new startup warnings |
| Option binding | Nested host.json path binds true/false using production options binding; default and formatted options are false |
| Option on but inactive/V1/None | One explanatory warning; no listener/import introduced |
| Option on, host absent / empty eligible list | Defined fallback/logging; no disabled tracing, lost record, or invented metadata |
| Host identity, not type-name guessing | The actual WebJobs invocation initializer is excluded; unrelated same-short-name custom type is not |
| Conditional client-IP initializer | Real `IHttpContextAccessor` registration present; unrelated HTTP client IP is not imported |
| Retained initializers | Real role initializer and representative Script host-instance enrichment retained; HTTP/metric-only entries do not corrupt Durable spans |
| Host ordering and multiplicity | Distinct same-type instances and intentionally repeated registrations retain order/multiplicity; no accidental extra invocation added |
| SDK correlation default | One effective exact default retained; custom subclass not removed |
| Naming precedence | Default name preserved; intentional custom operation name retained; existing instance-ID behavior on/off, missing-activity, entity and create-orchestration cases unchanged |
| Adversarial ambient context | Generated request name/success/result/correlation/timing preserved despite unrelated Activity Name/Succeeded tags and logger scope, with supported enrichers |
| Exceptions | Throwing initializer produces SDK diagnostic; later initializer and normal transmission still occur; no added retry or rollback |
| Mutable custom-code contract | Deliberate custom mutation remains possible; tests do not falsely imply a field-protection wrapper |
| Pipeline isolation | Host processors are not invoked on Durable records; host configuration/list/channel are not mutated; existing ordinary host telemetry is unchanged |
| Lifetime/concurrency | Host-aligned instance borrowing, concurrent initialization, startup snapshot membership, shutdown ordering, and disposal remain valid |

### End-to-end and release gates

1. Use a real Functions/WebJobs host with Application Insights registration and memory capture, not only an independently constructed configuration. Exercise the existing production constructor selection and startup snapshot boundary, including an initializer added through final host configuration.
2. Run an orchestration with an activity, failure, and replay, plus an entity scenario covering `WebJobsTelemetryModule`. Assert names, roles, properties, status, parent links, and unchanged raw per-span emission counts with the option off/on.
3. Capture both the host and Durable channels, including records outside the correlated root. The existing correlation helper filters by Durable names and sorts a trace subset; it must not be the sole oracle for absence of extra records.
4. Run with host configuration present/absent and Entra forwarding enabled/disabled, including the existing `OnSend` hook. Retain all existing authentication/channel ownership/flush regression coverage.
5. Verify Functions-host `ScriptTelemetryInitializer` retention and host-instance changes across host restart against a real supported host. Confirm worker-only custom initializers are not represented as host initializers.
6. Before release, use an Azure staging deployment to confirm ingestion and role-filtered presentation with the actual loaded host/SDK versions. Do not claim this spec's local probes satisfy that gate.

For targeted implementation validation, run `TelemetryActivatorTests`, `HostTelemetryInitializerTests`, naming tests, and options formatter/binding tests in `test\FunctionsV2\WebJobs.Extensions.DurableTask.Tests.V2.csproj`; include both target frameworks. Run correlation E2E scenarios with the repository's existing storage prerequisites.

## 10. Evidence references

- **[E1] Extension baseline:** [TelemetryActivator setup and V2 activation](https://github.com/Azure/azure-functions-durable-extension/blob/22264d83f911c776d06363fdb79e0f864d27b40d/src/WebJobs.Extensions.DurableTask/Correlation/TelemetryActivator.cs), [channel forwarding](https://github.com/Azure/azure-functions-durable-extension/blob/22264d83f911c776d06363fdb79e0f864d27b40d/src/WebJobs.Extensions.DurableTask/Correlation/HostForwardingTelemetryChannel.cs), [existing naming initializer](https://github.com/Azure/azure-functions-durable-extension/blob/22264d83f911c776d06363fdb79e0f864d27b40d/src/WebJobs.Extensions.DurableTask/Correlation/DurableTaskInstanceIdTelemetryInitializer.cs).
- **[E2] WebJobs 3.0.45:** [invocation-driven mutations](https://github.com/Azure/azure-webjobs-sdk/blob/v3.0.45/src/Microsoft.Azure.WebJobs.Logging.ApplicationInsights/Initializers/WebJobsTelemetryInitializer.cs#L42-L221), [role environment behavior](https://github.com/Azure/azure-webjobs-sdk/blob/v3.0.45/src/Microsoft.Azure.WebJobs.Logging.ApplicationInsights/Initializers/WebJobsRoleEnvironmentTelmetryInitializer.cs#L18-L83), [registrations and completed configuration](https://github.com/Azure/azure-webjobs-sdk/blob/v3.0.45/src/Microsoft.Azure.WebJobs.Logging.ApplicationInsights/Extensions/ApplicationInsightsServiceCollectionExtensions.cs#L182-L258).
- **[E3] Functions host `85a260d2` / v4.1054.200:** [Script initializer registration order](https://github.com/Azure/azure-functions-host/blob/85a260d24bbf53ad3899d2218e8422757513cda2/src/WebJobs.Script/ScriptHostBuilderExtensions.cs#L482-L495), [Script initializer behavior](https://github.com/Azure/azure-functions-host/blob/85a260d24bbf53ad3899d2218e8422757513cda2/src/WebJobs.Script/Config/ScriptTelemetryInitializer.cs#L10-L41), [separate worker registration](https://github.com/Azure/azure-functions-dotnet-worker/blob/84163a28ebdd1854b4dbf7f359c5c8dcc71c7dc3/src/DotNetWorker.ApplicationInsights/FunctionsApplicationInsightsExtensions.cs#L20-L61).
- **[E4] Application Insights 2.23.0:** [Track and per-initializer execution/diagnostics](https://github.com/microsoft/ApplicationInsights-dotnet/blob/2.23.0/BASE/src/Microsoft.ApplicationInsights/TelemetryClient.cs#L444-L593), [snapshot collection](https://github.com/microsoft/ApplicationInsights-dotnet/blob/2.23.0/BASE/src/Microsoft.ApplicationInsights/Extensibility/Implementation/SnapshottingCollection.cs#L35-L101), [operation-start reinitialization](https://github.com/microsoft/ApplicationInsights-dotnet/blob/2.23.0/BASE/src/Microsoft.ApplicationInsights/TelemetryClientExtensions.cs#L101-L125), [HTTP-context dependency](https://github.com/microsoft/ApplicationInsights-dotnet/blob/2.23.0/NETCORE/src/Microsoft.ApplicationInsights.AspNetCore/TelemetryInitializers/TelemetryInitializerBase.cs#L31-L52), [WebJobs final configuration order](https://github.com/Azure/azure-webjobs-sdk/blob/v3.0.45/src/Microsoft.Azure.WebJobs.Logging.ApplicationInsights/Extensions/ApplicationInsightsServiceCollectionExtensions.cs#L329-L410).
- **[E5] Historical broader integration:** [#3009](https://github.com/Azure/azure-functions-durable-extension/pull/3009), [#3053 revert](https://github.com/Azure/azure-functions-durable-extension/pull/3053). The revert establishes missing spans, not their precise production cause.
- **[E6] DurableTask AI 0.11.0:** [module conversion and single Track path](https://github.com/Azure/durabletask/blob/durabletask.applicationinsights-v0.11.0/src/DurableTask.ApplicationInsights/DurableTelemetryModule.cs#L52-L87).
