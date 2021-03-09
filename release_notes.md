
## New Features
- Added support to select a storage backend provider when multiple are installed (#1702): Select which storage backend to use by setting the `type` field under `durableTask/storageProvider` in host.json. If this field isn't set, then the storage backend will default to using Azure Storage.