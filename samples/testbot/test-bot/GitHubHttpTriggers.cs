// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DFTestBot
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class GitHubHttpTriggers
    {
        const string CommandPrefix = "/DFTest ";

        static readonly string TestAppSubscriptionId = Environment.GetEnvironmentVariable("TEST_APP_SUBSCRIPTION_ID");

        [FunctionName("GitHubComment")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "POST")] HttpRequest req,
            [DurableClient] IDurableClient durableClient,
            ILogger log)
        {
            log.LogInformation($"Received a webhook: {req.GetDisplayUrl()}");

            if (!req.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult("Expected application/json");
            }

            dynamic json;
            using (var reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                string content = await reader.ReadToEndAsync();
                try
                {
                    json = JObject.Parse(content);
                }
                catch (JsonReaderException e)
                {
                    return new BadRequestObjectResult($"Invalid JSON: {e.Message}");
                }
            }

            if (json?.issue?.pull_request == null)
            {
                return new BadRequestObjectResult("Not a pull request comment");
            }
            else if (json.action != "created" && json.action != "edited")
            {
                return new BadRequestObjectResult($"Not a new/edited comment (action = '{json.action}')");
            }

            string commentBody = json.comment.body;
            log.LogInformation($"Comment: {commentBody}");

            Uri commentApiUrl = new Uri((string)json.issue.comments_url);
            Uri commentIdApiUrl = new Uri((string)json.comment.url);

            // TODO: We should support multiple tests runs in a single comment at some point.

            bool startsWithDFTest = commentBody.StartsWith(CommandPrefix);
            if (!startsWithDFTest || commentBody.Contains("Durable Functions Test Bot"))
            {
                // Ignore unrelated comments or comments that come from the bot (like the help message)
                return new OkObjectResult($"No commands detected");
            }

            // get information about environment parameters if the user added any custom configuration
            string appPlanType = commentBody.Split(' ')[1];
            string skuType = "";
            string minCount = "";
            string maxCount = "";
            if (string.Equals(appPlanType, "ElasticPremium"))
            {
                ParsePremiumPlanParameters(commentBody, out string sku, out string minCountValue, out string maxCountValue);
                skuType = sku;
                minCount = minCountValue;
                maxCount = maxCountValue;
            }

            ParseOSTypeAndFunctionsVersion(commentBody, out string OSValue, out string functionsVersion);

            // string testCommand = "run";
            // int commandIndex = commentBody.IndexOf(testCommand);
            int commandStartIndex = commentBody.IndexOf(CommandPrefix, StringComparison.OrdinalIgnoreCase);
            string command = commentBody.Substring(commandStartIndex + CommandPrefix.Length); // commentBody.Substring(commandIndex);
            if (!TryParseCommand(command, out string friendlyTestName, out TestDescription testInfo, out string testParameters, out string errorMessage))
            {
                //await GitHubClient.PostCommentAsync(commentApiUrl, errorMessage, log);
                await GitHubClient.PatchCommentAsync(commentIdApiUrl, commentBody, errorMessage, log);
                return new OkObjectResult($"Replied with instructions");
            }

            var sb = new StringBuilder();
            sb.Append("Hi! I have received your command: ");
            sb.AppendLine($"`{command}`");
            sb.AppendLine();

            if (json.issue.author_association != "COLLABORATOR" &&
                json.issue.author_association != "OWNER")
            {
                sb.AppendLine($"Unfortunately, only collaborators are allowed to run these commands.");

                string internalMessage = $"Command {command} rejected because author_association = {json.issue.author_association}.";
                log.LogWarning(internalMessage);
                return new OkObjectResult(internalMessage);
            }

            Uri pullRequestUrl = new Uri((string)json.issue.pull_request.url);
            dynamic pullRequestJson = await GitHubClient.GetPullRequestInfoAsync(pullRequestUrl);
            string branchName = pullRequestJson.head.@ref;
            string commentAction = json.action;

            // NOTE: site names must be 60 characters or less, leaving ~24 characters for test names
            string shortenedTestName = friendlyTestName.Length >= 6 ? friendlyTestName.Substring(0, 6) : friendlyTestName;
            string issueId = json.issue.number;
            string dftestWithFriendlyName = $"dftest-{shortenedTestName}";
            string prNum = $"pr{issueId}";
            string dateTime = $"{DateTime.UtcNow:yyyyMMdd}";
            string guid = Guid.NewGuid().ToString().Substring(0, 4);
            
            string appName = $"{dftestWithFriendlyName}-{prNum}-{dateTime}-{guid}";
            string resourceGroupName = appName + "-rg";
            string storageAccountName = $"dftest{dateTime}{guid}sa";
            string appPlanName = appName + "-plan";

            var parameters = new TestParameters
            {
                SubscriptionId = TestAppSubscriptionId,
                ResourceGroup = resourceGroupName,
                StorageAccount = storageAccountName,
                GitHubCommentApiUrl = commentApiUrl,
                GitHubCommentIdApiUrl = commentIdApiUrl,
                GitHubCommentAction = commentAction,
                GitHubBranch = branchName,
                AppName = appName,
                AppPlanName = appPlanName,
                TestName = testInfo.StarterFunctionName,
                Parameters = testParameters,
                DetectorName = testInfo.AppLensDetector,
                AppPlanType = appPlanType,
                Sku = skuType,
                MinInstanceCount = minCount,
                MaxInstanceCount = maxCount,
                OSType = OSValue,
                FunctionsVersion = functionsVersion
            };

            string instanceId = $"DFTestBot-PR{issueId}-{DateTime.UtcNow:yyyyMMddhhmmss}";
            await durableClient.StartNewAsync(nameof(BotOrchestration.BotOrchestrator), instanceId, parameters);
            sb.Append($"I've started a new deployment orchestration with ID **{instanceId}** that will validate the changes in this PR. ");
            sb.AppendLine($"If the build succeeds, the orchestration will create an app named **{appName}** and run the _{friendlyTestName}_ test using your custom build.");
            sb.AppendLine();
            sb.AppendLine("These are the configuration options for the build:");
            if (string.Equals(appPlanType, "ElasticPremium"))
            {
                sb.AppendLine("**Elastic Premium Plan**");
                sb.AppendLine($"Sku = **{skuType}**, Minimum instance count = **{minCount}**, MaximumInstanceCount = **{maxCount}**");
            }
            else
            {
                sb.AppendLine("Consumption Plan");
            }
            sb.AppendLine($"OS = **{OSValue}**, Functions version = **{functionsVersion}**");

            sb.AppendLine();
            sb.AppendLine("I'll report back when I have status updates.");

            // await GitHubClient.PostCommentAsync(commentApiUrl, sb.ToString(), log);
            string currentCommentBody;
            if (commentAction.Equals("edited"))
            {
                // start a new test run without the logs from previous runs
                string endString = "end";
                int endIndex = commentBody.IndexOf(endString);
                currentCommentBody = commentBody.Substring(commandStartIndex, endIndex - commandStartIndex);
            }
            else
            {
                currentCommentBody = await GitHubClient.GetCommentBodyAsync(commentIdApiUrl, log);
            }
            
            await GitHubClient.PatchCommentAsync(commentIdApiUrl, currentCommentBody, sb.ToString(), log);
            log.LogInformation("Test scheduled successfully!");
            return new OkObjectResult("Test scheduled successfully!");
        }

        private static void ParsePremiumPlanParameters(string commentBody, out string sku, out string minCountValue, out string maxCountValue)
        {
            int skuValueIndex = commentBody.IndexOf("sku");
            int minCountIndex = commentBody.IndexOf("minCount");
            int maxCountIndex = commentBody.IndexOf("maxCount");

            sku = "EP1";
            minCountValue = "1";
            maxCountValue = "3";

            if (skuValueIndex >= 0)
            {
                sku = commentBody.Substring(skuValueIndex).Split(' ')[1];
            }

            if (minCountIndex >= 0)
            {
                minCountValue = commentBody.Substring(minCountIndex).Split(' ')[1];
            }

            if (maxCountIndex >= 0)
            {
                maxCountValue = commentBody.Substring(maxCountIndex).Split(' ')[1];
            }
        }

        private static void ParseOSTypeAndFunctionsVersion(string commentBody, out string oSType, out string functionsVersion)
        {
            int osTypeIndex = commentBody.IndexOf("os");
            oSType = "Windows";

            if (osTypeIndex >= 0)
            {
                oSType = commentBody.Substring(osTypeIndex).Split(' ')[1];
            }

            // get functions version
            int functionsVersionValueIndex = commentBody.IndexOf("functionsVersion");
            functionsVersion = "3";

            if (functionsVersionValueIndex >= 0)
            {
                functionsVersion = commentBody.Substring(functionsVersionValueIndex).Split(' ')[1];
            }
        }

        static bool TryParseCommand(
            string input,
            out string testName,
            out TestDescription testInfo,
            out string testParameters,
            out string errorMessage)
        {
            string[] parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                testName = null;
                testInfo = null;
                testParameters = null;
                errorMessage = GetSyntaxHelp() + Environment.NewLine + Environment.NewLine + GetTestNameHelp();
                return false;
            }

            string runString = "run";
            string endString = "end";
            int runIndex = input.IndexOf(runString);
            int endIndex = input.IndexOf(endString);

            // if (parts.Length < 2 || !parts[0].Equals("run", StringComparison.OrdinalIgnoreCase))
            if (parts.Length < 2 || runIndex < 0 || endIndex < 0)
            {
                testName = null;
                testInfo = null;
                testParameters = null;
                errorMessage = GetSyntaxHelp();
                return false;
            }
            parts = input.Substring(runIndex, endIndex-runIndex).Split(' ');
            testName = parts[1];
            testParameters = string.Join('&', parts[2..]);
            if (SupportedTests.TryGetTestInfo(testName, out testInfo))
            {
                errorMessage = null;
                return true;
            }
            else
            {
                errorMessage = GetTestNameHelp();
                return false;
            }
        }

        static string GetSyntaxHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"The syntax for a test with a Consumption plan is: `{CommandPrefix.Trim()} Consumption os <os type> functionsVersion <version> run <TestName> <Param1> <Param2> ...`");
            sb.AppendLine($"Example: `{CommandPrefix.Trim()} Consumption os Windows functionsVersion 3 run ManySequences`");
            sb.AppendLine();
            sb.AppendLine($"The syntax for a test with an Elastic Premium plan is: `{CommandPrefix.Trim()} ElasticPremium sku <skuValue> minCount <count> maxCount <count> os <os type> functionsVersion <version> run <TestName> <Param1> <Param2> ...`");
            sb.AppendLine($"Example: `{CommandPrefix.Trim()} ElasticPremium sku EP1 minCount 1 maxCount 3 os Windows functionsVersion 3 run ManySequences`");
            return sb.ToString();
        }

        static string GetTestNameHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Here are the supported `<TestName>` values:").AppendLine();
            foreach ((string name, TestDescription description) in SupportedTests.GetAll())
            {
                sb.Append($"* `{name}`: {description.Description}");
                if (!description.IsEnabled)
                {
                    sb.Append(" **(DISABLED)**");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}