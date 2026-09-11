<!-- Please include a link to your pending docs PR below. https://docs.microsoft.com/en-us/azure/azure-functions/durable ([private docs repo for Microsoft employees](http://github.com/MicrosoftDocs/azure-docs-pr)). 
Your code PR should not be merged until your docs PR has been approved. Wait to #sign-off until release. -->

https://github.com/MicrosoftDocs/azure-docs-pr/pull/320038

Pending companion update: document the unreleased
`IServiceCollection.AddDurableTaskTelemetryInitializer` API for in-process apps.
Include explicit instance/factory registration, V2-only lazy resolution, initializer
order and ownership, host versus isolated-worker registration, privacy and processor
boundaries, restart/rollback, and the first released package version.
The in-repo sample README contains the proposed user guidance. Public docs approval
remains a merge gate for this feature.
