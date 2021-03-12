## New Features
- Added support to select a storage backend provider when multiple are installed (#1702): Select which storage backend to use by setting the `type` field under `durableTask/storageProvider` in host.json. If this field isn't set, then the storage backend will default to using Azure Storage.
- Improved concurrency defaults for the App Service Consumption plan (https://github.com/Azure/azure-functions-durable-extension/pull/1706)
- Improved supportability logs on Linux Dedicated plans (https://github.com/Azure/azure-functions-durable-extension/pull/1721)

## Bug Fixes:
- Properly used update management API URLs after a successful slot swap (#1716)