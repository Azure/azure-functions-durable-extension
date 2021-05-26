## New Features
- Improved supportability logs on Linux Dedicated plans (#1721)
- Improved concurrency defaults in Consumption plans for C# and JS (#1846)
- Initial work to simplify out-of-process execution model (#1836)
- Mutliple concurrent start requests to a singleton orchestration instance should now only start a single instance ((#528)[https://github.com/Azure/durabletask/pull/528])

## Bug fixes
- Fix issue with local RPC endpoint used by non-.NET languages on Windows apps (#1800)
- Emit warning instead of blocking startup if Distributed Tracing is enabled, but `APPINSIGHTS_INSTRUMENTATIONKEY` isn't set (#1787),
- Assign cloud_RoleName and operation_Name fields to RequestTelemetry to populate Activity Function's Invocations List when Distributed Tracing is enabled (#1808)
- Fix Linux telemetry for new durablity providers (#1848)

## Breaking Changes