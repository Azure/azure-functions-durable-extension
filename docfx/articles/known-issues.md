# Known-Issues / FAQ

## Host Startup Errors
If you're trying to run durable functions and the host is failing to start, check the host logs to see if it contains hints about the problem. A few examples:

> error CS0006: Metadata file 'Microsoft.Azure.WebJobs.Extensions.DurableTask' could not be found

This means the **Durable Task** extension has not been properly installed. See the [installation guide](./installation.md) for instructions on how to do the extension setup.
