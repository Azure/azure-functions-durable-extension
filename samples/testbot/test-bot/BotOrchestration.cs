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
        static readonly string DeploymentServiceKey = Environment.GetEnvironmentVariable("DEPLOYMENT_SERVICE_API_KEY");

        [FunctionName(nameof(BotOrchestrator))]
        public static async Task BotOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            log = context.CreateReplaySafeLogger(log);
            
            DateTime startTimeUtc = context.CurrentUtcDateTime;
            TestParameters testParameters = context.GetInput<TestParameters>();

            // Create a new resource group
            if (!await TryCallDeploymentServiceHttpApiAsync("api/CreateNewResourceGroup", context, log, testParameters))
            {
                string message = $"Failed to create a new resource group! 💣 Check the internal deployment service logs for more details.";
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                throw new Exception(message);
            }

            // Check if the resource group was created and ready to use
            while (!await TryCallDeploymentServiceHttpApiAsync("api/CheckResourceGroupStatus", context, log, testParameters))
            {
                // Retry every 10 seconds
                await SleepAsync(context, TimeSpan.FromSeconds(10));
            }

            await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, "Successfully created a new resource group."));

            // Create storage account
            if (!await TryCallDeploymentServiceHttpApiAsync("api/CreateNewStorageAccount", context, log, testParameters))
            {
                string message = $"Failed to create a new storage account! 💣 Check the internal deployment service logs for more details.";
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                throw new Exception(message);
            }

            // Check if the storage account was created and ready to use
            while (!await TryCallDeploymentServiceHttpApiAsync("api/CheckStorageAccountStatus", context, log, testParameters))
            {
                // Retry every 10 seconds
                await SleepAsync(context, TimeSpan.FromSeconds(10));
            }

            await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, "Successfully created a new storage account."));

            if (string.Equals(testParameters.AppPlanType, "ElasticPremium"))
            {
                // Create a new function app plan
                if (!await TryCallDeploymentServiceHttpApiAsync("api/CreateNewFunctionAppPlan", context, log, testParameters))
                {
                    string message = $"Failed to create a new function app plan! 💣 Check the internal deployment service logs for more details.";
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                    throw new Exception(message);
                }

                // Check if the function app plan was created and ready to use
                while (!await TryCallDeploymentServiceHttpApiAsync("api/CheckFunctionAppPlanStatus", context, log, testParameters))
                {
                    // Retry every 10 seconds
                    await SleepAsync(context, TimeSpan.FromSeconds(10));
                }

                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, "Successfully created a new function app plan."));

                // Create a new function app with plan
                if (!await TryCallDeploymentServiceHttpApiAsync("api/CreateNewFunctionAppWithPlan", context, log, testParameters))
                {
                    string message = $"Failed to create a new function app! 💣 Check the internal deployment service logs for more details.";
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                    throw new Exception(message);
                }
                else
                {
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, "Successfully created a new function app on an existing plan."));
                }
            }
            else
            {
                // Create a new function app
                if (!await TryCallDeploymentServiceHttpApiAsync("api/CreateNewFunctionApp", context, log, testParameters))
                {
                    string message = $"Failed to create a new function app! 💣 Check the internal deployment service logs for more details.";
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                    throw new Exception(message);
                }
                else
                {
                    await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, "Successfully created a new function app."));
                }
            }

            try
            {
                // Deploy and start the test app
                HttpManagementPayload managementUrls = null;
                if (!await TryCallDeploymentServiceHttpApiAsync(
                    "api/DeployToFunctionApp",
                    context,
                    log,
                    testParameters,
                    (responseJson) => managementUrls = JsonConvert.DeserializeObject<HttpManagementPayload>(responseJson)))
                {
                    string message = $"Failed to deploy the test app! 💣 Check the internal deployment service logs for more details.";
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
                        // TODO: look at if this post comment is necessary
                        // await context.CallActivityAsync(nameof(PostGitHubComment), (testParameters.GitHubCommentApiUrl, currentCustomStatus));
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
                // Example: https://applens.azurewebsites.net/subscriptions/92d757cd-ef0d-4710-967d-2efa3c952358/resourceGroups/perf-testing/providers/Microsoft.Web/sites/dfperf-dedicated2/detectors/DurableFunctions_ManySequencesTest?startTime=2020-08-22T00:00&endTime=2020-08-22T01:00

                // AppLens time range restrictions: Start and End date time must not be more than 24 hrs apart
                //                                  Start and End date time must be at least 15 minutes apart
                //                                  Start date must be within the past 30 days
                //                                  End date must be 15 minutes less than the current time
                //                                  Start date time must be 30 minutes less than current date time

                DateTime endTimeUtc = context.CurrentUtcDateTime.AddMinutes(5);
                if (endTimeUtc.Subtract(startTimeUtc).TotalMinutes < 15)
                {
                    startTimeUtc = endTimeUtc.AddMinutes(-15);
                }

                string startTime = startTimeUtc.ToString("yyyy-MM-ddTHH:mm");
                string endTime = endTimeUtc.ToString("yyyy-MM-ddTHH:mm");
                string resultsAvailableTime = (endTimeUtc.AddMinutes(15)).ToString("yyyy-MM-dd HH:mm UTC");

                string subscriptionId = testParameters.SubscriptionId;
                string resourceGroup = testParameters.ResourceGroup;
                string appName = testParameters.AppName;
                string detectorName = testParameters.DetectorName;

                string link = $"https://applens.azurewebsites.net/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{appName}/detectors/{detectorName}?startTime={startTime}&endTime={endTime}";

                string appLensLinkString = $"You can view more detailed results in [AppLens]({link}) (Microsoft internal). The results will be available at {resultsAvailableTime}🔗📈";
                finalMessage += Environment.NewLine + Environment.NewLine + appLensLinkString;
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, finalMessage));

                /*
                // TODO: resource group clean up functionality
                TimeSpan cleanupInterval = TimeSpan.FromHours(1);
                DateTime cleanupTime = context.CurrentUtcDateTime.Add(cleanupInterval);
                finalMessage += Environment.NewLine + Environment.NewLine + $"The test app **{appName}** will be deleted at {cleanupTime:yyyy-MM-dd hh:mm:ss} UTC.";

                await context.CallActivityAsync(nameof(PostGitHubComment), (testParameters.GitHubCommentApiUrl, finalMessage));

                log.LogInformation($"Sleeping until {cleanupTime:yyyy-MM-dd hh:mm:ss} UTC to delete the test function app.");
                await context.CreateTimer(cleanupTime, CancellationToken.None);
                */
            }
            catch (Exception)
            {
                string message = $"An unexpected failure occurred! 💣 Unfortunately, we can't continue the test run and the test app will be deleted. 😞";
                await context.CallActivityAsync(nameof(PatchGitHubComment), (testParameters.GitHubCommentIdApiUrl, message));
                throw;
            }
            finally
            {
                /*   
                // TODO: add functionality to delete the resource group when the PR is merged or closed

                // Any intermediate failures should result in an automatic cleanup
                log.LogInformation($"Deleting the test function app, {testParameters.AppName}.");
                if (!await TryCallDeploymentServiceHttpApiAsync("api/DeleteFunctionApp", context, log, testParameters))
                {
                    string failureMessage = $"Failed to delete the test app! 💣 Check the internal deployment service logs for more details.";
                    await context.CallActivityAsync(nameof(PostGitHubComment), (testParameters.GitHubCommentApiUrl, failureMessage));
                    throw new Exception(failureMessage);
                }

                string cleanupSuccessMessage = $"The test app {testParameters.AppName} has been deleted. Thanks for using the DFTest bot!";
                await context.CallActivityAsync(nameof(PostGitHubComment), (testParameters.GitHubCommentApiUrl, cleanupSuccessMessage));
                */
            }

        }

        static async Task<bool> TryCallDeploymentServiceHttpApiAsync(
            string httpApiPath,
            IDurableOrchestrationContext context,
            ILogger log,
            TestParameters testParameters,
            Action<string> handleResponsePayload = null)
        {
            Uri deploymentServiceUri = testParameters.IsStagingTest ? DeploymentServiceStagingBaseUrl : DeploymentServiceBaseUrl;

            var request = new DurableHttpRequest(
                HttpMethod.Post,
                new Uri(deploymentServiceUri, httpApiPath),
                headers: new Dictionary<string, StringValues>
                {
                    { "x-functions-key", DeploymentServiceKey },
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