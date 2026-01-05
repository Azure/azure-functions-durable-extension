# Precompiled C# Durable Functions Samples

This folder contains precompiled C# samples demonstrating various Durable Functions patterns. These samples use the Azure Functions In-Process model with the Azure WebJobs SDK.

## Patterns Demonstrated

| Sample File | Pattern | Description |
|-------------|---------|-------------|
| [HelloSequence.cs](HelloSequence.cs) | Function Chaining | Sequentially calls activity functions, where each output can be input to the next |
| [BackupSiteContent.cs](BackupSiteContent.cs) | Fan-out/Fan-in | Executes multiple activity functions in parallel and waits for all to complete |
| [Monitor.cs](Monitor.cs) | Monitor | Polls for a condition with configurable intervals and timeout |
| [PhoneVerification.cs](PhoneVerification.cs) | Human Interaction | Waits for external events (human input) with timeout handling |
| [Counter.cs](Counter.cs) | Aggregator (Durable Entity) | Stateful entity that maintains state across operations |
| [SMSReminder.cs](SMSReminder.cs) | Durable Timer | Uses durable timers for delayed/scheduled execution |
| [RestartVMs.cs](RestartVMs.cs) | Managed Identity HTTP | Makes authenticated HTTP calls using managed identity |

## Getting Started

### Prerequisites

- [.NET Core SDK](https://dotnet.microsoft.com/download) 3.1 or later
- [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local) v3.x or later
- [Azure Storage Emulator](https://docs.microsoft.com/azure/storage/common/storage-use-emulator) or an Azure Storage account

### Building the Sample

```bash
dotnet build VSSample.sln
```

Or open `VSSample.sln` in Visual Studio and build from there.

### Running the Sample Locally

1. Configure `local.settings.json` with your Azure Storage connection string:

   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet"
     }
   }
   ```

2. Start the function app:

   ```bash
   func host start
   ```

   Or press F5 in Visual Studio.

### Running the Orchestrations

Use the following cURL commands to start different orchestrations:

**Function Chaining (HelloSequence):**
```bash
curl -i -X POST http://localhost:7071/orchestrators/E1_HelloSequence -H "Content-Length: 0"
```

**Fan-out/Fan-in (BackupSiteContent):**
```bash
curl -i -X POST http://localhost:7071/orchestrators/E2_BackupSiteContent -H "Content-Length: 0"
```

**Aggregator/Entity (Counter):**
```bash
# Reset counter
curl -X POST -H "Content-Length: 0" "http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Reset"

# Add to counter
curl -d "5" -X POST -H "Content-Type: application/json" http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter?op=Add

# Get counter value
curl http://localhost:7071/runtime/webhooks/durabletask/entities/Counter/MyCounter
```

### Checking Orchestration Status

The POST response includes a `Location` header and a `statusQueryGetUri` in the JSON body. Use this URL to check the orchestration status:

```bash
curl -i "http://localhost:7071/runtime/webhooks/durabletask/instances/{instanceId}?taskHub=TestHubName&connection=Storage"
```

## Samples Requiring External Services

Some samples require external service configuration:

### Monitor, PhoneVerification, SMSReminder (Twilio)

These samples require a [Twilio](https://www.twilio.com) account. Add the following app settings:

- `TwilioAccountSid`: Your Twilio account SID
- `TwilioAuthToken`: Your Twilio auth token
- `TwilioPhoneNumber`: Your Twilio SMS-capable phone number

### RestartVMs (Azure Managed Identity)

This sample requires:
- Function App with managed identity enabled
- Appropriate Azure RBAC permissions to list and restart VMs

## Documentation

For more information on Durable Functions patterns, see the [official documentation](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview):

- [Function Chaining](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-sequence)
- [Fan-out/Fan-in](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-cloud-backup)
- [Monitor](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-monitor)
- [Human Interaction](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-phone-verification)
- [Durable Entities](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-entities)
- [Durable Timers](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-timers)
- [Durable HTTP](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-http-features)
