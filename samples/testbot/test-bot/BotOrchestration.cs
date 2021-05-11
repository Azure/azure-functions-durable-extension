// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace DFTestBot
{
    public static class BotOrchestration
    {
        // NOTE: Using environment variables in orchestrator functions is not safe since environment variables are non-deterministic.
        //       I'm ignoring this advice for now for the sake of expediency
        static readonly Uri DeploymentServiceBaseUrl = new Uri(Environment.GetEnvironmentVariable("DEPLOYMENT_SERVICE_BASE_URL"));
        static readonly Uri DeploymentServiceStagingBaseUrl = new Uri(Environment.GetEnvironmentVariable("DEPLOYMENT_SERVICE_STAGING_BASE_URL"));

        [FunctionName(nameof(BotOrchestrator))]
        public static async Task BotOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            log = context.CreateReplaySafeLogger(log);
            
            DateTime startTimeUtc = context.CurrentUtcDateTime;
            TestParameters testParameters = context.GetInput<TestParameters>();

            // Create a new resource group
            if (!await TryCreateNewResource(context, log, testParameters, "CreateNewResourceGroup", "CheckResourceGroupStatus", "Failed to create a new resource group!", "Successfully created a new resource group!"))
            {
                throw new Exception("Failed to create a new resource group!");
            }

            // Create a new storage account
            if (!await TryCreateNewResource(context, log, testParameters, "CreateNewStorageAccount", "CheckStorageAccountStatus", "Failed to create a new storage account!", "Successfully created a new storage account!"))
            {
                throw new Exception("Failed to create a new storage account!");
            }

            if (string.Equals(testParameters.AppPlanType, "ElasticPremium"))
            {
                // Create a new function app plan
                if (!await TryCreateNewResource(context, log, testParameters, "CreateNewFunctionAppPlan", "CheckFunctionAppPlanStatus", "Failed to create a new function app plan!", "Successfully created a new function app plan!"))
                {
                    throw new Exception("Failed to create a new storage account!");
                }

                // Create a new function app with plan
                if (!await TryCreateNewResource(context, log, testParameters, "CreateNewFunctionAppWithPlan", null, "Failed to create a new function app!", "Successfully created a new function app on an existing plan!"))
                {
                    throw new Exception("Failed to create a new storage account!");
                } 
            }
            else
            {
                // Create a new function app
                if (!await TryCreateNewResource(context, log, testParameters, "CreateNewFunctionApp", null, "Failed to create a new function app!", "Successfully created a new function app!"))
                {
                    throw new Exception("Failed to create a new storage account!");
                }
            }

            // Deploy code to the function app
            if (!await TryCallDeploymentServiceHttpApiAsync("DeployToFunctionApp", context, log, testParameters))
            {
                string message = $"Failed to deploy code to the function app! 💣 Check the internal deployment service logs for more details.";
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                throw new Exception(message);
            }
            else
            {
                string message = "Successfully deployed code to the function app!" + Environment.NewLine + Environment.NewLine + $"Sending request to {testParameters.TestName}";
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
            }

            try
            {
                // Trigger the test function
                HttpManagementPayload managementUrls = null;
                if (!await TryCallDeploymentServiceHttpApiAsync(
                    "TriggerTestFunction",
                    context,
                    log,
                    testParameters,
                    (responseJson) => managementUrls = JsonConvert.DeserializeObject<HttpManagementPayload>(responseJson)))
                {
                    string message = $"Failed to trigger the test function! 💣 Check the internal deployment service logs for more details.";
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                    throw new Exception(message);
                }

                // Get the URL for doing status queries
                if (managementUrls == null || string.IsNullOrEmpty(managementUrls.StatusQueryGetUri))
                {
                    string message = $"The deployment service API call succeeded but returned an unexpected response. Check the logs for details.";
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                    throw new Exception(message);
                }

                await SleepAsync(context, TimeSpan.FromMinutes(1));
                DurableOrchestrationStatus status = await WaitForStartAsync(context, log, managementUrls);
                
                if (status == null)
                {
                    string message = $"The test was scheduled but still hasn't started! Giving up. 😞";
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                    throw new Exception(message);
                }

                string previousCustomStatus = string.Empty;
                while (true)
                {
                    // The test orchestration reports back using a string message in the CustomStatus field
                    string currentCustomStatus = (string)status.CustomStatus;
                    if (currentCustomStatus != previousCustomStatus)
                    {
                        // There is a new status update - post it back to the PR thread.
                        log.LogInformation($"Current test status: {currentCustomStatus}");
                        previousCustomStatus = currentCustomStatus;
                    }

                    if (status.RuntimeStatus != OrchestrationRuntimeStatus.Running)
                    {
                        // The test orchestration completed.
                        break;
                    }

                    // Check every minute for an update - we don't want to poll too frequently or else the
                    // history will build up too much.
                    await SleepAsync(context, TimeSpan.FromMinutes(1));

                    // Refesh the status
                    status = await GetStatusAsync(context, managementUrls, log);
                }

                // The test orchestration has completed
                string finalMessage = status.RuntimeStatus switch
                {
                    OrchestrationRuntimeStatus.Completed => "The test completed successfully! ✅",
                    OrchestrationRuntimeStatus.Failed => "The test failed! 💣",
                    OrchestrationRuntimeStatus.Terminated => "The test was terminated or timed out. ⚠",
                    _ => $"The test stopped unexpectedly. Runtime status = **{status.RuntimeStatus}**. 🤔"
                };

                // Generate the AppLens link
                DateTime endTimeUtc = context.CurrentUtcDateTime.AddMinutes(5);
                string resultsAvailableTime = CalculateResultsAvailableTime(context, startTimeUtc, endTimeUtc);
                string link = GenerateAppLensLink(context, testParameters, startTimeUtc, endTimeUtc);

                string appLensLinkString = $"You can view more detailed results in [AppLens]({link}) (Microsoft internal). The results will be available at {resultsAvailableTime}🔗📈";
                finalMessage += Environment.NewLine + Environment.NewLine + appLensLinkString;
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, finalMessage));
            }
            catch (Exception)
            {
                string message = $"An unexpected failure occurred! 💣 Unfortunately, we can't continue the test run and the test app will be deleted. 😞";
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                throw;
            }

        }

        static string CalculateResultsAvailableTime(IDurableOrchestrationContext context, DateTime startTimeUtc, DateTime endTimeUtc)
        {
            // AppLens time range restrictions: Start and End date time must not be more than 24 hrs apart
            //                                  Start and End date time must be at least 15 minutes apart
            //                                  Start date must be within the past 30 days
            //                                  End date must be 15 minutes less than the current time
            //                                  Start date time must be 30 minutes less than current date time

            
            if (endTimeUtc.Subtract(startTimeUtc).TotalMinutes < 15)
            {
                startTimeUtc = endTimeUtc.AddMinutes(-15);
            }

            string resultsAvailableTime = (endTimeUtc.AddMinutes(15)).ToString("yyyy-MM-dd HH:mm UTC");
            return resultsAvailableTime;
        }

        static string GenerateAppLensLink(IDurableOrchestrationContext context, TestParameters testParameters, DateTime startTimeUtc, DateTime endTimeUtc)
        {
            // Generate the AppLens link
            // Example: https://applens.azurewebsites.net/subscriptions/92d757cd-ef0d-4710-967d-2efa3c952358/resourceGroups/perf-testing/providers/Microsoft.Web/sites/dfperf-dedicated2/detectors/DurableFunctions_ManySequencesTest?startTime=2020-08-22T00:00&endTime=2020-08-22T01:00

            string subscriptionId = testParameters.SubscriptionId;
            string resourceGroup = testParameters.ResourceGroup;
            string appName = testParameters.AppName;
            string detectorName = testParameters.DetectorName;

            string startTime = startTimeUtc.ToString("yyyy-MM-ddTHH:mm");
            string endTime = endTimeUtc.ToString("yyyy-MM-ddTHH:mm");

            string link = $"https://applens.azurewebsites.net/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{appName}/detectors/{detectorName}?startTime={startTime}&endTime={endTime}";
            return link;
        }

        static async Task<bool> TryCreateNewResource(
            IDurableOrchestrationContext context,
            ILogger log,
            TestParameters testParameters,
            string deploymentServiceCreateFuncName,
            string deploymentServiceCheckStatusFuncName,
            string errorMessage,
            string successMessage)
        {
            // Create the resource
            if (!await TryCallDeploymentServiceHttpApiAsync(deploymentServiceCreateFuncName, context, log, testParameters))
            {
                string message = $"{errorMessage} 💣 Check the internal deployment service logs for more details.";
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                return false;
            }

            if (deploymentServiceCheckStatusFuncName != null)
            {
                // Check if the resource was created and is ready to use
                while (!await TryCallDeploymentServiceHttpApiAsync(deploymentServiceCheckStatusFuncName, context, log, testParameters))
                {
                    // Retry every 10 seconds
                    await SleepAsync(context, TimeSpan.FromSeconds(10));
                }
            }

            await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, successMessage));
            return true;
        }

        static async Task<bool> TryCallDeploymentServiceHttpApiAsync(
            string functionName,
            IDurableOrchestrationContext context,
            ILogger log,
            TestParameters testParameters,
            Action<string> handleResponsePayload = null)
        {
            Uri deploymentServiceUri = testParameters.IsStagingTest ? DeploymentServiceStagingBaseUrl : DeploymentServiceBaseUrl;

            string httpApiPath = $"api/{functionName}";
            string deploymentFunctionKey = await GetFunctionKey(functionName, context, testParameters, log);

            var request = new DurableHttpRequest(
                HttpMethod.Post,
                new Uri(deploymentServiceUri, httpApiPath),
                headers: new Dictionary<string, StringValues>
                {
                    { "x-functions-key", deploymentFunctionKey },
                    { "Content-Type", "application/json" },
                },
                content: JsonConvert.SerializeObject(testParameters));

            // Deploy and start the test app
            log.LogInformation($"Calling deployment service: {request.Method} {request.Uri}...");
            DurableHttpResponse response = await context.CallHttpAsync(request);
            log.LogInformation($"Received response from deployment service: {response.StatusCode}: {response.Content}");
            if ((int)response.StatusCode < 300)
            {
                handleResponsePayload?.Invoke(response.Content);
                return true;
            }

            return false;
        }

        private static async Task<string> GetFunctionKey(string functionName, IDurableOrchestrationContext context, TestParameters testParameters, ILogger log)
        {
            string subscriptionId = testParameters.SubscriptionId;
            string resourceGroupName = "dfdeploymentservice";
            string siteName = "dfdeploymentservice";
            string slot = "staging";

            string listKeyProdUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName}/functions/{functionName}/listkeys?api-version=2019-08-01";
            string listKeySlotUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{siteName}/slots/{slot}/functions/{functionName}/listkeys?api-version=2019-08-01";
            string listKeysUrl = testParameters.IsStagingTest ? listKeySlotUrl : listKeyProdUrl;

            log.LogInformation($"listKeysUrl = {listKeysUrl}");

            var getFunctionKeysRequest = new DurableHttpRequest(
                HttpMethod.Post,
                new Uri(listKeysUrl),
                tokenSource: new ManagedIdentityTokenSource("https://management.core.windows.net/.default"));

            DurableHttpResponse getFunctionKeysResponse = await context.CallHttpAsync(getFunctionKeysRequest);
            string functionsKey = "";
            try
            {
                log.LogInformation($"getFunctionKeysResponse.Content = {getFunctionKeysResponse.Content}");
                functionsKey = JsonConvert.DeserializeObject<Dictionary<string, string>>(getFunctionKeysResponse.Content)["default"];
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
                log.LogInformation(e.InnerException.ToString());
                log.LogInformation(e.StackTrace);
            }
            return functionsKey;
        }

        [FunctionName(nameof(PostGitHubComment))]
        public static Task PostGitHubComment([ActivityTrigger] (Uri commentApiUrl, string markdownMessage) input, ILogger log)
        {
            return GitHubClient.PostCommentAsync(
                input.commentApiUrl,
                input.markdownMessage,
                log);
        }

        [FunctionName(nameof(PatchGitHubComment))]
        public static async Task PatchGitHubComment([ActivityTrigger] (Uri commentIdApiUrl, string markdownMessage) input, ILogger log)
        {
            // get comment's body
            string currentCommentBody = await GitHubClient.GetCommentBodyAsync(
                input.commentIdApiUrl,
                log);

            // update the comment with the new message
            await GitHubClient.PatchCommentAsync(
                input.commentIdApiUrl,
                currentCommentBody,
                input.markdownMessage,
                log);
        }

        static async Task<DurableOrchestrationStatus> WaitForStartAsync(
            IDurableOrchestrationContext context,
            ILogger log,
            HttpManagementPayload managementUrls)
        {
            log.LogInformation($"Waiting for {managementUrls.Id} to start.");
            DateTime timeoutTime = context.CurrentUtcDateTime.AddMinutes(5);
            while (true)
            {
                log.LogInformation("WAITFORSTART: Getting function's status...");
                DurableOrchestrationStatus status = await GetStatusAsync(context, managementUrls, log);
                log.LogInformation($"WAITFORSTART: Received the status: {status}");
                log.LogInformation($"WAITFORSTART: RuntimeStatus = {status.RuntimeStatus}");

                if (status != null && status.RuntimeStatus != OrchestrationRuntimeStatus.Pending)
                {
                    // It started - break out of the loop
                    log.LogInformation($"Instance {managementUrls.Id} started successfully.");
                    return status;
                }

                if (context.CurrentUtcDateTime >= timeoutTime)
                {
                    // Timeout - return null to signal that it never started
                    log.LogWarning($"Instance {managementUrls.Id} did not start in {timeoutTime}. Giving up.");
                    return null;
                }

                // Retry every 10 seconds
                await SleepAsync(context, TimeSpan.FromSeconds(10));
            }
        }

        static async Task<DurableOrchestrationStatus> GetStatusAsync(
            IDurableOrchestrationContext context,
            HttpManagementPayload managementUrls,
            ILogger log)
        {
            log.LogInformation("GETSTATUSASYNC: Sending a request to statusQueryGetUri.");
            DurableHttpResponse res = await context.CallHttpAsync(
                HttpMethod.Get,
                new Uri(managementUrls.StatusQueryGetUri));

            log.LogInformation($"GETSTATUSASYNC: Received status from statusQueryGetUri. status: {res.Content}");

            try
            {
                return JsonConvert.DeserializeObject<DurableOrchestrationStatus>(res.Content);
            }
            catch (Exception e)
            {
                log.LogError("GETSTATUSASYNC: Throwing exception when deserializing response content.", e.GetType(), e.Message, e.InnerException);
                throw;
            }
        }

        static Task SleepAsync(IDurableOrchestrationContext context, TimeSpan sleepTime)
        {
            return context.CreateTimer(
                context.CurrentUtcDateTime.Add(sleepTime),
                CancellationToken.None);
        }
    }
}