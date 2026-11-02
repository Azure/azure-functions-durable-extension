<!-- Please include a link to your pending docs PR below. https://docs.microsoft.com/en-us/azure/azure-functions/durable ([private docs repo for Microsoft employees](http://github.com/MicrosoftDocs/azure-docs-pr)). 
Your code PR should not be merged until your docs PR has been approved. Wait to #sign-off until release. -->

https://github.com/MicrosoftDocs/azure-docs-pr/pull/320038

https://github.com/MicrosoftDocs/azure-docs-pr/pull/320514

Draft documentation for the unreleased
`IServiceCollection.AddDurableTaskTelemetryInitializer` API for in-process apps.

- [ ] Companion docs PR approved (required before merging this feature).

Draft documentation for the unreleased `allowExternalActivities` host.json
setting (`durableTask`, default `false`), for the Durable Task Scheduler work
item filtering page: when enabled, HTTP-protocol orchestrators (Python, Node.js,
PowerShell) can schedule activities that are not registered in the app, so a
worker sharing the task hub can run them. The setting only skips the local name
check; it does not route work. With work item filtering enabled, an activity
that no worker hosts stays pending. Without filtering, the app itself may
receive the work item and fail it.

- [ ] Companion docs PR approved (required before merging this feature).

Publication and #sign-off remain deferred until release.
