# Explicit telemetry initializers for Durable distributed tracing V2

**Status:** Implemented on this branch. Public documentation approval and real Functions-host/Azure staging validation remain merge and release gates.

**Issue:** [#1792 - Duplicate App Insight Telemetry](https://github.com/Azure/azure-functions-durable-extension/issues/1792).

## 1. Problem and scope

Durable distributed tracing V2 uses a private Application Insights `TelemetryConfiguration`. This preserves Durable's tracing pipeline and correlation independently from the Functions host, but it also means telemetry initializers registered only with host Application Insights do not customize Durable requests and dependencies.

This change adds an explicit registration destination for Durable V2 telemetry initializers. It addresses the customization and enrichment portion of #1792. It does not merge the host and Durable pipelines, suppress host function telemetry, establish the cause of every historical duplicate record, or close #1792.

The previous branch design automatically copied selected initializers from the host configuration and excluded concrete types considered unsafe. That design was rejected because the extension cannot reliably classify arbitrary current and future initializers by concrete type. Explicit registration makes application intent the selection mechanism.

## 2. Public API

The extension adds two overloads:

```csharp
IServiceCollection AddDurableTaskTelemetryInitializer(
    this IServiceCollection services,
    ITelemetryInitializer initializer);

IServiceCollection AddDurableTaskTelemetryInitializer(
    this IServiceCollection services,
    Func<IServiceProvider, ITelemetryInitializer> factory);
```

The instance overload supports an existing standard initializer instance. The factory overload supports resolving a singleton-compatible initializer and its dependencies from the completed host service provider. Null services, instances, factories, and factory results are rejected.

No new marker interface is needed. Registered values remain ordinary Application Insights `ITelemetryInitializer` implementations.

## 3. Runtime behavior

When distributed tracing is enabled with version V2, `TelemetryActivator`:

1. Creates Durable's existing private `TelemetryConfiguration`, including its existing SDK defaults.
2. Resolves the explicit Durable initializer factories once, in registration order.
3. Appends every returned initializer, preserving deliberate registration multiplicity.
4. Appends the existing `DurableTaskInstanceIdTelemetryInitializer`.
5. Starts the existing Durable V2 telemetry listeners.

The resulting order is:

```text
Durable V2 module creates telemetry from its Activity
  -> private Application Insights SDK defaults
  -> explicitly registered Durable initializers, in registration order
  -> Durable operation-name/instance-ID initializer
  -> private processors
  -> existing private or host-forwarding channel
```

With no explicit registrations, the released Durable tracing behavior and initializer list are unchanged. Registrations do not activate tracing. Factories are not resolved when tracing is disabled or the configured version is V1 or None.

The host `TelemetryConfiguration` remains available to `TelemetryActivator` only for the existing Microsoft Entra authenticated channel reuse. Its initializers, processors, modules, sampling, redaction, and `TelemetryClient.Context` are not scanned or copied.

## 4. Lifetime and ownership

The activator snapshots registrations for its host generation and resolves each factory at most once after V2 activation. A repeated activator initialization reuses the same initializer instances.

Durable borrows registered initializer instances. It does not:

- dispose them;
- initialize them as telemetry modules;
- clone them;
- build another service provider; or
- resolve them from a scoped service provider.

Factories must return singleton-compatible, host-lifetime instances. Prefer resolving an instance already owned by dependency injection. A factory must not create an orphaned disposable initializer, and it must not capture a scoped dependency. Applications can also pass an externally owned instance to both host Application Insights and Durable registration.

Changing initializer registrations requires a host restart. Removing the Durable registration and restarting restores the released Durable-only initializer behavior.

## 5. Initializer contract

Explicit initializers run through the normal Application Insights SDK path. They remain trusted mutable code and may deliberately change properties, operation names, IDs, success, sampling fields, or routing fields. The extension does not restore fields around an initializer.

Initializers must be thread-safe, tolerate repeated `Initialize` calls, and support request and dependency telemetry emitted without a live HTTP request or function invocation. Application Insights continues to diagnose an initializer exception and run later initializers. Registration and factory failures surface during V2 activation rather than silently disabling customization.

The final Durable initializer retains existing naming behavior. It supplies a missing operation name and, when enabled, appends the orchestration instance ID after custom initializers have run.

## 6. Pipeline and process boundaries

Only explicitly registered initializers participate. In particular:

- host-only initializers do not automatically run on Durable telemetry;
- Durable registrations do not run on host telemetry unless separately registered there;
- initializers registered only in a .NET isolated worker cannot customize host-process Durable spans;
- host processors, filters, redaction, and sampling do not protect or transform the private Durable records; and
- forwarding through the host channel does not re-run host initializers or processors.

This integration does not add a telemetry client, tracking call, exporter, channel, module, or Activity listener. Raw emitted record count and channel ownership remain unchanged.

## 7. Validation

Automated coverage must establish:

- no registration preserves the existing private initializer list;
- host-only initializers do not leak into Durable;
- instance and service-provider factory registration use the real `HostBuilder`/`AddDurableTask` path;
- a shared initializer can be registered explicitly with both destinations;
- order and multiplicity are preserved;
- inactive tracing does not resolve factories;
- null results and factory exceptions surface;
- custom mutations and SDK exception continuation remain ordinary SDK behavior;
- initializers are thread-safe/non-owned and are not initialized as modules;
- host processors and client context remain isolated; and
- the E2E raw span count, identities, replay/failure/entity behavior, and both private/forwarded channel paths are unchanged with registration off and on.

Public docs and Azure staging validation remain required before release.
