// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/* This sample demonstrates how to make authenticated HTTP calls using Managed Identity.
 * The orchestrator uses CallHttpAsync with a ManagedIdentityTokenSource to automatically
 * acquire and refresh Azure AD tokens for calling Azure Resource Manager APIs.
 *
 * This example lists all VMs in a subscription and restarts them sequentially.
 *
 * Documentation:
 * https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-http-features
 *
 * To run this sample:
 *   1. Enable managed identity on your Azure Function App
 *   2. Grant the managed identity permissions to your subscription (e.g., VM Contributor role)
 *   3. Start the function app
 *   4. Make an HTTP POST request to: http://localhost:7071/api/RestartVMs_HttpStart
 *      Include JSON body: {"subscriptionId": "your-subscription-id", "resourceGroup": "your-resource-group"}
 *
 * Required setup:
 *   - Function App must have a system-assigned or user-assigned managed identity
 *   - The identity must have permissions to list and restart VMs in the target subscription
 */
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VSSample
{
    public static class RestartVMs
    {
        // Orchestrator function that uses managed identity to call Azure Resource Manager APIs.
        // Demonstrates durable HTTP with automatic token acquisition and refresh.
        [FunctionName("RestartVMs")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            ResourceInfo vmInfo = context.GetInput<ResourceInfo>();
            string apiVersion = vmInfo.ApiVersion ?? "2018-06-01";
            string subscriptionId = vmInfo.SubscriptionId;
            string resourceGroup = vmInfo.ResourceGroup;

            // Implicitly uses the Azure AD identity of the current app to make an HTTP call to Azure Resource Manager
            var managedIdentity = new ManagedIdentityTokenSource("https://management.core.windows.net/.default");

            // List all of the VMs in my subscription and add them to a list.
            DurableHttpRequest request = new DurableHttpRequest(
                HttpMethod.Get,
                new Uri($"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Compute/virtualMachines?api-version=2018-06-01"),
                tokenSource: managedIdentity);
            DurableHttpResponse response = await context.CallHttpAsync(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new ArgumentException($"Failed to list VMs: {response.StatusCode}: {response.Content}");
            }

            // Deserializes content to just get the names of the VMs in the subscription
            JObject jObject = JsonConvert.DeserializeObject<JObject>(response.Content);
            var vmNamesList = new List<string>();
            foreach (JToken value in jObject["value"])
            {
                string vmName = value["name"].ToString();
                vmNamesList.Add(vmName);
            }

            // Restart all of the VMs in my subscription
            foreach (string vmName in vmNamesList)
            {
                var restartRequest = new DurableHttpRequest(
                    HttpMethod.Post, 
                    new Uri($"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}/restart?api-version={apiVersion}"),
                    tokenSource: managedIdentity);
                DurableHttpResponse restartResponse = await context.CallHttpAsync(restartRequest);
                if (restartResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new ArgumentException($"Failed to restart VM: {restartResponse.StatusCode}: {restartResponse.Content}");
                }
            }
        }

        // HTTP trigger function to start the VM restart orchestration.
        // Validates input and provides an example payload for error cases.
        [FunctionName("RestartVMs_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            ResourceInfo vmInfo = await req.Content.ReadAsAsync<ResourceInfo>();
            if (vmInfo == null || vmInfo.SubscriptionId == null || vmInfo.ResourceGroup == null)
            {
                var example = new ResourceInfo
                {
                    SubscriptionId = "4c51f150-5b69-4cda-aa7a-88a9ac297393",
                    ResourceGroup = "my-resource-group"
                };

                var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
                response.Content = new StringContent("A request payload is required. Example: " + JsonConvert.SerializeObject(example, Formatting.None));
                return response;
            }

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("RestartVMs", vmInfo);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        // Input model for the orchestration containing Azure subscription information
        class ResourceInfo
        {
            [JsonProperty("apiVersion", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string ApiVersion { get; set; }

            [JsonProperty("subscriptionId")]
            public string SubscriptionId { get; set; }

            [JsonProperty("resourceGroup")]
            public string ResourceGroup { get; set; }
        }
    }
}