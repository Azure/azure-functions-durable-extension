// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    // To authorize ARM calls, your subscription has to have permissions to your function app.
    // Making the subscription an owner of your function app is one solution to this.
    public static class RestartVMs
    {
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