# WebJobs Extension Protobuf

This directory contains the protobuf definitions for the WebJobs Extension SDK, which are used to generate the C# source code for the gRPC service contracts. The official protobuf definitions are maintained in the [Durable Task Protobuf repository](https://github.com/microsoft/durabletask-protobuf).

## Updating the Protobuf Definitions

To update the protobuf definitions in this directory, follow these steps:

1. Make sure you have [PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed on your machine.
2. Run the following command to download the latest protobuf definitions from the Durable Task Protobuf repository:

```powershell
.\refresh-protos.ps1
```

This script will download the latest protobuf definitions from the `https://github.com/microsoft/durabletask-protobuf` repository and copy them to this directory.

By default, the latest versions of the protobufs are downloaded from the `main` branch. To specify an alternative branch, use the `-branch` parameter:

```powershell
.\refresh-protos.ps1 -branch <branch-name>
```

The `versions.txt` file in this directory contains the list of protobuf files and their commit hashes that were last downloaded. It is updated automatically by the `refresh-protos.ps1` script.