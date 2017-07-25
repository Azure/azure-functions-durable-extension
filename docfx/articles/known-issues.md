# Known-Issues / FAQ
In general, all known issues should be tracked in the [GitHub issues](https://github.com/Azure/azure-functions-durable-extension/issues) list. If you run into an issue and are looking for a resolution, search here first. Make sure to check both active and closed issues since it's possible that the issue has been fixed in a more recent version of the extension or sample app.

If you can't find the issue mentioned in the existing list of issues, feel free to open a new issue. Even if you simply want to ask a question, feel free to open a GitHub issue and tag it as a question. We'll add common questions to this page.

## Host Startup Errors
If you're trying to run durable functions and the host is failing to start, check the host logs to see if it contains hints about the problem. A few examples:

> error CS0006: Metadata file 'Microsoft.Azure.WebJobs.Extensions.DurableTask' could not be found

This means the **Durable Task** extension has not been properly installed. See the [installation guide](./installation.md) for instructions on how to do the extension setup.

## HTTP 500 responses from the "HttpStart" sample trigger
This likely means that the `HttpStart` sample function failed to deserialize the payload of the request. It can be caused by one of the following problems:
1. There is content in the POST body but the request does not have a `Content-Type: application/json` header.
2. The POST body is not properly formatted/encoded JSON.
3. You're using an old version of the Visual Studio sample app which is missing the `WEBSITE_HOSTNAME` app setting in `local.appsettings.json`.

## ObjectDisposedException when starting a durable function
When attempting to start a new durable function, it may fail to run and the following error may appear in your logs:

```
Microsoft.Azure.WebJobs.Host.FunctionInvocationException : Exception while executing function: Functions.MyDurableFunction ---> System.InvalidOperationException : Exception binding parameter 'log' ---> System.ObjectDisposedException : Cannot access a disposed object.
Object name: 'LoggerFactory'.
   at Microsoft.Extensions.Logging.LoggerFactory.CreateLogger(String categoryName)
   at Microsoft.Azure.WebJobs.Host.Bindings.TraceWriterBinding.BindAsync(BindingContext context)
   at async Microsoft.Azure.WebJobs.Host.Triggers.TriggeredFunctionBinding`1.BindCoreAsync[TTriggerValue](ValueBindingContext context,Object value,IDictionary`2 parameters) 
   End of inner exception
   at Microsoft.Azure.WebJobs.Host.Executors.DelayedException.Throw()
   at async Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.ExecuteWithWatchersAsync(IFunctionInstance instance,IReadOnlyDictionary`2 parameters,TraceWriter traceWriter,ILogger logger,CancellationTokenSource functionCancellationTokenSource)
   at async Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.ExecuteWithLoggingAsync(??)
   at async Microsoft.Azure.WebJobs.Host.Executors.FunctionExecutor.ExecuteWithLoggingAsync(??) 
   End of inner exception
```

This is believed to be a bug in the core Azure Functions runtime, currently tracked [here](https://github.com/Azure/azure-webjobs-sdk-script/issues/1690). The workaround is to set the `FUNCTIONS_EXTENSION_VERSION` app setting to `1.0.11015` and restart the function app.