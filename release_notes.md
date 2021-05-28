## New Features
- Added an App Lease feature to allow users to control when they want to swap their app to be the primary and take over the App Lease using .Net API MakeCurrentAppPrimaryAsync and HTTP API /makeprimary. The previous lease owner app will automatically reset its state once it's lost the lease and attempt to acquire the App Lease once again, removing the manual burden of the App Lease feature. (https://github.com/Azure/azure-functions-durable-extension/pull/1769 https://github.com/Azure/durabletask/pull/529)
- Improved supportability logs on Linux Dedicated plans (https://github.com/Azure/azure-functions-durable-extension/pull/1721)
- Improved concurrency defaults in Consumption plans for C# and JS (https://github.com/Azure/azure-functions-durable-extension/pull/1846)
- Initial work to simplify out-of-process execution model (https://github.com/Azure/azure-functions-durable-extension/pull/1836)
- Mutliple concurrent start requests to a singleton orchestration instance should now only start a single instance ((#528)[https://github.com/Azure/durabletask/pull/528])

## Bug fixes
- Fix issue with local RPC endpoint used by non-.NET languages on Windows apps (#1800)
- Emit warning instead of blocking startup if Distributed Tracing is enabled, but `APPINSIGHTS_INSTRUMENTATIONKEY` isn't set (#1787),
- Assign cloud_RoleName and operation_Name fields to RequestTelemetry to populate Activity Function's Invocations List when Distributed Tracing is enabled (#1808)
- Fix Linux telemetry for new durablity providers (#1848)
- Update dependencies toa ddress CVE-2019-0548 and CVE-2021-26701 vulnerabilities (#1789)

## Breaking Changes