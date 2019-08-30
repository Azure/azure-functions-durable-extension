using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
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
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            string apiVersion = await context.CallActivityAsync<string>("GetApiVersion", null);
            string subscriptionId = await context.CallActivityAsync<string>("GetSubscriptionId", null);
            string resourceGroup = await context.CallActivityAsync<string>("GetResourceGroup", null);

            ManagedIdentityTokenSource managedIdentityTokenSource = new ManagedIdentityTokenSource("https://management.core.windows.net");

            // List all of the VMs in my subscription and add them to an ArrayList
            string listAllCallString = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Compute/virtualMachines?api-version={apiVersion}";
            Uri listAllUri = new Uri(listAllCallString);
            DurableHttpRequest listRequest = new DurableHttpRequest(method: HttpMethod.Get, uri: listAllUri, tokenSource: managedIdentityTokenSource);
            DurableHttpResponse listAllResponse = await context.CallHttpAsync(listRequest);

            // Deserializes content to just get the names of the VMs in the subscription
            JObject jObject = JsonConvert.DeserializeObject<JObject>(listAllResponse.Content);
            ArrayList vmNamesList = new ArrayList();
            foreach (var value in jObject["value"])
            {
                string vmName = value["name"].ToString();
                vmNamesList.Add(vmName);
            }

            // Restart all of the VMs in my subscription
            foreach (string vmName in vmNamesList)
            {
                var restartVMCallString = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}/restart?api-version={apiVersion}";
                DurableHttpRequest restartRequest = new DurableHttpRequest(method: HttpMethod.Post, uri: new Uri(restartVMCallString), tokenSource: managedIdentityTokenSource);
                DurableHttpResponse restartResponse = await context.CallHttpAsync(restartRequest);
            }
        }

        [FunctionName("GetApiVersion")]
        public static string GetApiVersion([ActivityTrigger] string name, ILogger log)
        {
            // Get API Version from environment variables
            return Environment.GetEnvironmentVariable("ApiVersion", EnvironmentVariableTarget.Process);
        }

        [FunctionName("GetSubscriptionId")]
        public static string GetSubscriptionId([ActivityTrigger] string name, ILogger log)
        {
            // Get subscription Id from environment variables
            return Environment.GetEnvironmentVariable("SubscriptionId", EnvironmentVariableTarget.Process);
        }

        [FunctionName("GetResourceGroup")]
        public static string GetResourceGroup([ActivityTrigger] string name, ILogger log)
        {
            // Get resource group from environment variables
            return Environment.GetEnvironmentVariable("ResourceGroup", EnvironmentVariableTarget.Process);
        }

        private static void Log(
        string statement,
        IDurableOrchestrationContext context,
        ILogger log)
        {
            if (!context.IsReplaying)
            {
                log.LogInformation(statement);
            }
        }

        [FunctionName("RestartVMs_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("RestartVMs", null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}