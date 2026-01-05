// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates how to start orchestrations via HTTP.
 * The HttpStart function is a generic HTTP trigger that can start any orchestration
 * by name. It returns the standard Durable Functions response containing URLs for
 * checking status, sending events, and terminating the orchestration.
 *
 * To run this sample:
 *   1. Start the function app locally using `func host start` or run from Visual Studio
 *   2. Start an orchestration by posting to:
 *      curl -i -X POST http://localhost:7071/orchestrators/{functionName} -H "Content-Length: 0"
 *      Replace {functionName} with the name of the orchestration to start (e.g., E1_HelloSequence)
 *   3. Optionally include JSON input in the request body
 *
 * The response includes a Location header and URLs for managing the orchestration instance.
 *
 * No special app settings are required for this sample.
 */
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace VSSample
{
    public static class HttpStart
    {
        // HTTP trigger function that starts an orchestration by name.
        // Uses route parameter to determine which orchestration to start.
        [FunctionName("HttpStart")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "orchestrators/{functionName}")] HttpRequestMessage req,
            [DurableClient] IDurableClient starter,
            string functionName,
            ILogger log)
        {
            // Function input comes from the request content.
            object eventData = await req.Content.ReadAsAsync<object>();
            string instanceId = await starter.StartNewAsync(functionName, eventData);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            // CreateCheckStatusResponse returns the standard Durable Functions response
            // containing status query URLs and management endpoints
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
