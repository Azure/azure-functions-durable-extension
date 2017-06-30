# Known-Issues / FAQ

## Host Startup Errors
If you're trying to run durable functions and the host is failing to start, check the host logs to see if it contains hints about the problem. A few examples:

> error CS0006: Metadata file 'Microsoft.Azure.WebJobs.Extensions.DurableTask' could not be found

This means the **Durable Task** extension has not been properly installed. See the [installation guide](./installation.md) for instructions on how to do the extension setup.

## HTTP 500 responses from the "HttpStart" sample trigger
This likely means that the `HttpStart` sample function failed to deserialize the payload of the request. It can be caused by one of two problems:
1. There is content in the POST body but the request does not have a `Content-Type: application/json` header.
2. The POST body is not properly formatted/encoded JSON.

This sample HTTP triggers is very sensitive to request content that is not clearly JSON content. A future version may improve on this.