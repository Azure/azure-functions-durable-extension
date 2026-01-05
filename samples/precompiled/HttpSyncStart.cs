// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates synchronous HTTP orchestration starting.
 * Unlike HttpStart which returns immediately with status URLs, this function
 * waits for the orchestration to complete before returning the result.
 * This is useful when orchestrations complete quickly and you want to get
 * the result in a single HTTP request.
 *
 * To run this sample:
 *   1. Start the function app locally using `func host start` or run from Visual Studio
 *   2. Start an orchestration and wait for completion:
 *      curl -i -X POST "http://localhost:7071/orchestrators/{functionName}/wait" -H "Content-Length: 0"
 *      Replace {functionName} with the name of the orchestration to start (e.g., E1_HelloSequence)
 *   3. Optional query parameters:
 *      - timeout: Maximum seconds to wait (default: 30)
 *      - retryInterval: Seconds between status checks (default: 1)
 *
 * If the orchestration completes within the timeout, the result is returned directly.
 * If it doesn't complete in time, the standard status check URLs are returned instead.
 *
 * No special app settings are required for this sample.
 */
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace VSSample
{
    public static class HttpSyncStart
    {
        private const string Timeout = "timeout";
        private const string RetryInterval = "retryInterval";

        // HTTP trigger function that starts an orchestration and waits for it to complete.
        // Returns the orchestration result if it completes within the timeout.
        [FunctionName("HttpSyncStart")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, methods: "post", Route = "orchestrators/{functionName}/wait")]
            HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            string functionName,
            ILogger log)
        {
            // Function input comes from the request content.
            object eventData = await req.Content.ReadAsAsync<object>();
            string instanceId = await starter.StartNewAsync(functionName, eventData);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            // Parse optional timeout and retry interval from query string
            TimeSpan timeout = GetTimeSpan(req, Timeout) ?? TimeSpan.FromSeconds(30);
            TimeSpan retryInterval = GetTimeSpan(req, RetryInterval) ?? TimeSpan.FromSeconds(1);
            
            // Wait for the orchestration to complete, or return status URLs if it takes too long
            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(
                req,
                instanceId,
                timeout,
                retryInterval);
        }

        // Helper method to parse TimeSpan from query string parameters
        private static TimeSpan? GetTimeSpan(HttpRequestMessage request, string queryParameterName)
        {
            string queryParameterStringValue = request.RequestUri.ParseQueryString()[queryParameterName];
            if (string.IsNullOrEmpty(queryParameterStringValue))
            {
                return null;
            }

            return TimeSpan.FromSeconds(double.Parse(queryParameterStringValue));
        }
    }
}
