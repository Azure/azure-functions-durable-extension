## New Features

## Bug fixes

* Fix message loss bug in ContinueAsNew scenarios ([Azure/durabletask#544](https://github.com/Azure/durabletask/pull/544))
* Allow custom connection string names when creating a DurableClient in an ASP.NET Core app (external app) (#1895)
* Instead of endlessly retrying deleted, disabled, or renamed orchestrator and activity functions, we will now fail any in-flight executions if a function with that name no longer exists. (#1901)
* Improve API documentation regarding uncancelled timers (#1903)

## Breaking Changes