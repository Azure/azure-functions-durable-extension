# Reference 

You can get correlation information from these classes. 

## CorrelationTraceContext

CorrelationTraceContext share the current TraceContext.

### Properties

| Property | Type | Description |
| -------- | -----|------ |
| Current  | TraceContextBase |Get or Set the current TraceContext with AsyncLocal |

## TraceContextBase

Represents TraceContext Base class, that preserve the Correlation information. 

### Properties

| Property | Type | Description |
| -------- | -----|------ |
| StartTime  | DateTimeOffset |Get or Set the start time of this telemetry |
| TelemetryType | TlemetryType | Get or Set the telemetry type. Possible values are RequestTelemetry or DepdendencyTelemetry |
| OrchestrationTraceContexts | `Stack<TraceContextBase>` | Get or Set the TraceContextBase object with Stack structure. This object represent Orchestration TraceContexts. It includes both Request and Dependnecy telemetries. |
| OperationName | string | Get or Set Operation name |

### Methods

| Method | Type | Description |
| -------- | -----|------ |
| GetCurrentOrchestrationRequestTraceContext()  | TraceContextBase | GetRequestTraceContext of CurrentOrchestration |


## W3CTraceContext

W3CTraceContext represent TraceContext for [W3CTraceContext](https://www.w3.org/TR/trace-context/) protocol. Implementation of the TraceContextBase. 

### Properties

| Property | Type | Description |
| -------- | -----|------ |
| TraceParent  | string |Get or Set the traceparent |
| TraceState | string | Get or Set the tracestate |
| ParentSpanId | string | Get or Set the ParentSpanId |
| Duration | TimeSpan | Get the duration of this extecution |
| TelemetryId | string | Get the telemetryId. This value is sent to the Application Insights. |
| TelemetryContextOperationId | string | Get the TelemetryContextOperationId. This value is sent to the Application Insights. |
| TelemetryContextOperationParentId | string | Get the TelemetryContextOperationParentId. This value is sent to the Application Insights. |


## HttpCorrelationTraceContext
HttpCorrelationTraceContext represent TraceContext for [HttpCorrelationProtocol](https://github.com/dotnet/runtime/blob/4f9ae42d861fcb4be2fcd5d3d55d5f227d30e723/src/libraries/System.Diagnostics.DiagnosticSource/src/HttpCorrelationProtocol.md) protocol. Implementation of the TraceContextBase. 

### Properties

| Property | Type | Description |
| -------- | -----|------ |
| ParentId  | string |Get or Set the ParentId |
| ParentParentId | string | Get or Set the ParentId of the parent |
| Duration | TimeSpan | Get the duration of this extecution |
| TelemetryId | string | Get the telemetryId. This value is sent to the Application Insights. |
| TelemetryContextOperationId | string | Get the TelemetryContextOperationId. This value is sent to the Application Insights. |
| TelemetryContextOperationParentId | string | Get the TelemetryContextOperationParentId. This value is sent to the Application Insights. |

## Activity

[Activity Class](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activity?view=netcore-3.1) represents an operation with context to be used for logging. If you want to get correlation information on Activity Functions, please refer to this object with `Activity.Current` property. For more details, please refer to [Activity User Guide](https://github.com/dotnet/runtime/blob/4f9ae42d861fcb4be2fcd5d3d55d5f227d30e723/src/libraries/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md1). 

## Samples 

### Get TraceContextBase object on an orchestrator function

```csharp
        [FunctionName(nameof(Orchestration_W3C))]
        public async Task<List<string>> Orchestration_W3C(
           [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            var correlationContext = CorrelationTraceContext.Current as W3CTraceContext;
            var trace = new TraceTelemetry(
                $"Activity Id: {correlationContext?.TraceParent} ParentSpanId: {correlationContext?.ParentSpanId}");
            trace.Context.Operation.Id = correlationContext?.TelemetryContextOperationId;
            trace.Context.Operation.ParentId = correlationContext?.TelemetryContextOperationParentId;
            _telemetryClient.Track(trace);

            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>(nameof(Hello_W3C), "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(Hello_W3C), "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(Hello_W3C), "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }
```

### Get Activity on Activity functions

```csharp
        [FunctionName(nameof(Hello_W3C))]
        public string Hello_W3C([ActivityTrigger] string name, ILogger log)
        {
            // Send Custom Telemetry
            var currentActivity = Activity.Current;
            _telemetryClient.TrackTrace($"Message from Activity: {name}.");

            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }
```

