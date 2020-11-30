# Configuration 

## host.json

### durableTask

#### tracing

| property | Default | Description |
| -------- | ------- | ----------- |
| DistributedTracingEnabled  | true | Make it true if you don't need Distributed Tracing for Durable Functions |
| DistributedTracingProtocol  | HttpCorrelationProtocol | Set Protocol of Distributed Tracing. Possible values are HttpCorrelationProtocol and W3CTraceContext |

**NOTE:** You need to specify the same protocol as `logging.applicationInsights.httpAutoCollectionOptions.enableW3CDistributedTracing`

### Sample

Enable Distributed Tracing with W3C Trace Context. 

_host.json_

```json

  "extensions": {
    "durableTask": {
      "tracing": {
        "DistributedTracingProtocol": "W3CTraceContext"
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

You need to specify the Application Insights Instrumentation key on AppSettings or local.settings.json or Environment Variables. 


_local.settings.json_ 

```json
{
    "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "APPINSIGHTS_INSTRUMENTATIONKEY": "<YOUR_INSTRUMENTATIONKEY>"
  }
}
```
