<!-- Please put your changes into the appropriate category (or categories) below. -->

## New Features
- Exposed IServiceCollection extension methods AddDurableTaskFactory() for net461 releases so classic .NET Framework apps using the .NET Core model of dependency injection can create their own Durable Clients. (#1653)

## Bug fixes
- Remove incorrect information from C# docs summary for IDurableEntityClient.ReadEntityStateAsync() regarding states large than 16KB (#1637)
- Fix a NullReferenceException in IDurableClient.SignalClient() for IDurableClient objects created by the new DurabilityClientFactory (#1644)

## Breaking changes


## Dependency Changes 
- Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers --> 0.4.0

Removed dependency on Mono.Posix.NETStandard by instead relying on P/Invoke to generate inotify signals in Linux; reducing the size of the package (#1643)
