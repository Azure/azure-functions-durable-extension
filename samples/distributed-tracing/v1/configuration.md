# Configuration

## host.json

### durableTask

#### tracing

| Property | Default | Description |
| -------- | ------- | ----------- |
| distributedTracingEnabled  | `false` | When set to `true`, enables the Distributed Tracing feature. |
| distributedTracingProtocol  | HttpCorrelationProtocol | Sets the protocol used by the Distributed Tracing feature. Possible values are `HttpCorrelationProtocol` and `W3CTraceContext` |

**NOTE:** You need to specify the same protocol as `logging.applicationInsights.httpAutoCollectionOptions.enableW3CDistributedTracing`

### Sample

The following example host.json file enables distributed tracing with the W3C trace context protocol.

_host.json_

```json

  "extensions": {
    "durableTask": {
      "tracing": {
        "distributedTracingEnabled": true,
        "distributedTracingProtocol": "W3CTraceContext"
      }
    }
  },
  "logging": {
    "applicationInsights": {
      "httpAutoCollectionOptions": {
        "enableW3CDistributedTracing": true
      }
    }
  },
  "version": "2.0"
}
```

## AppSettings

You need to specify the Application Insights instrumentation key in your app settings, local.settings.json, or environment variables. The name of the setting is `APPINSIGHTS_INSTRUMENTATIONKEY`.

_local.settings.json_ 

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "APPINSIGHTS_INSTRUMENTATIONKEY": "<YOUR_INSTRUMENTATION_KEY>"
  }
}
```
