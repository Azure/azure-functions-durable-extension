// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace DFTestBot
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
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
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "POST")] HttpRequest req,
            [DurableClient] IDurableClient durableClient,
            ILogger log)
        {
            log.LogInformation($"Received a webhook: {req.GetDisplayUrl()}");

            // Validate that the HttpRequest Content is JSON
            if (!req.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                log.LogInformation("Expected application/json");
                return;
            }

            // Deserialize the json payload into a GitHubRequestPayload
            GitHubRequestPayload gitHubRequestPayload;
            using (var reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                string content = await reader.ReadToEndAsync();
                try
                {
                    gitHubRequestPayload = JsonConvert.DeserializeObject<GitHubRequestPayload>(content);
                }
                catch (JsonReaderException e)
                {
                    log.LogInformation($"Invalid JSON: {e.Message}");
                    return;
                }
            }
            string commentBody = gitHubRequestPayload.Comment.Body;
            log.LogInformation($"Comment: {commentBody}");

            Uri commentApiUrl = new Uri((string)gitHubRequestPayload.Issue.Comments_Url);
            Uri commentIdApiUrl = new Uri((string)gitHubRequestPayload.Comment.Url);

            string currCommandPrefix = CommandPrefix;
            string stagingCommandPrefix = "/DFStagingTest";
            bool isStagingTest = false;
            if (commentBody.StartsWith(stagingCommandPrefix))
            {
                currCommandPrefix = stagingCommandPrefix;
                isStagingTest = true;
            }

            int commandStartIndex = commentBody.IndexOf(currCommandPrefix, StringComparison.OrdinalIgnoreCase);
            string command = commentBody.Substring(commandStartIndex + currCommandPrefix.Length);

            var sb = new StringBuilder();
            sb.AppendLine("🤖**Durable Functions Test Bot**🤖");
            sb.AppendLine();
            sb.Append("Hi! I have received your command: ");
            sb.AppendLine($"`{command}`");
            sb.AppendLine();

            string payloadAuthorAssociation = gitHubRequestPayload?.Issue?.Author_Association;
            if (!string.Equals(payloadAuthorAssociation, "COLLABORATOR") &&
                !string.Equals(payloadAuthorAssociation, "OWNER") &&
                !string.Equals(payloadAuthorAssociation, "MEMBER"))
            {
                sb.AppendLine($"Unfortunately, only owners, collaborators, and members are allowed to run these commands.");

                string internalMessage = $"Command {command} rejected because author_association = {payloadAuthorAssociation}.";
                log.LogWarning(internalMessage);
                await GitHubClient.PatchCommentAsync(commentIdApiUrl, commentBody, sb.ToString(), log);
                return;
            }

            if (!TryParseCommand(command, out string friendlyTestName, out TestDescription testInfo, out string testParameters, out string errorMessage))
            {
                await GitHubClient.PatchCommentAsync(commentIdApiUrl, commentBody, errorMessage, log);
                log.LogInformation("Replied with instructions");
                return;
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

            ParseOSTypeRuntimeFunctionsVersion(commentBody, out string OSValue, out string runtime, out string functionsVersion);

            Uri pullRequestUrl = new Uri(gitHubRequestPayload.Issue.Pull_Request.Url);
            dynamic pullRequestJson = await GitHubClient.GetPullRequestInfoAsync(pullRequestUrl);
            string branchName = pullRequestJson.head.@ref;
            string commentAction = gitHubRequestPayload.Action;

            // NOTE: site names must be 60 characters or less, leaving ~24 characters for test names
            string shortenedTestName = friendlyTestName.Length >= 6 ? friendlyTestName.Substring(0, 6) : friendlyTestName;
            string issueId = gitHubRequestPayload.Issue.Number;
            string dftestWithFriendlyName = $"dftest-{shortenedTestName}";
            string prNum = $"pr{issueId}";
            string dateTime = $"{DateTime.UtcNow:yyyyMMdd}";
            string guid = Guid.NewGuid().ToString().Substring(0, 4);

            string appName = $"{dftestWithFriendlyName}-{prNum}-{dateTime}-{guid}";
            string resourceGroupName = appName + "-rg";
            string storageAccountName = $"dftest{dateTime}{guid}sa";
            string appPlanName = appName + "-plan";

            string dirPath = testInfo.DirPath;
            string framework = "";

            if (runtime.Equals("dotnet"))
            {
                if (dirPath == null)
                {
                    dirPath = functionsVersion.Equals("1") ? "test\\DFPerfScenariosV1" : "test\\DFPerfScenarios";
                }
                framework = functionsVersion.Equals("1") ? "net461" : "netcoreapp3.1";
            }

            var parameters = new TestParameters
            {
                SubscriptionId = TestAppSubscriptionId,
                ResourceGroup = resourceGroupName,
                StorageAccount = storageAccountName,
                GitHubCommentApiUrl = commentApiUrl,
                GitHubCommentIdApiUrl = commentIdApiUrl,
                GitHubCommentAction = commentAction,
                GitHubBranch = branchName,
                HttpPath = testInfo.HttpPath,
                ProjectFileDirPath = dirPath,
                Framework = framework,
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
                Runtime = runtime,
                FunctionsVersion = functionsVersion,
                IsStagingTest = isStagingTest
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

        private static void ParseOSTypeRuntimeFunctionsVersion(string commentBody, out string oSType, out string runtime, out string functionsVersion)
        {
            // get OS type
            int osTypeIndex = commentBody.IndexOf("os");
            oSType = "Windows";

            if (osTypeIndex >= 0)
            {
                oSType = commentBody.Substring(osTypeIndex).Split(' ')[1];
            }

            // get runtime
            int runtimeIndex = commentBody.IndexOf("runtime");
            runtime = "dotnet";

            if (runtimeIndex > 0)
            {
                runtime = commentBody.Substring(runtimeIndex).Split(' ')[1];
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
                errorMessage = "🤖**Durable Functions Test Bot**🤖" + Environment.NewLine + GetSyntaxHelp() + Environment.NewLine + Environment.NewLine + GetTestNameHelp();
                return false;
            }

            string runString = "run";
            string endString = "end";
            int runIndex = input.IndexOf(runString);
            int endIndex = input.IndexOf(endString);

            if (parts.Length < 2 || runIndex < 0 || endIndex < 0)
            {
                testName = null;
                testInfo = null;
                testParameters = null;
                errorMessage = "🤖**Durable Functions Test Bot**🤖" + Environment.NewLine + GetSyntaxHelp();
                return false;
            }
            parts = input.Substring(runIndex, endIndex - runIndex).Split(' ');
            testName = parts[1];
            testParameters = string.Join('&', parts[2..]);
            if (SupportedTests.TryGetTestInfo(testName, out testInfo))
            {
                errorMessage = null;
                return true;
            }
            else
            {
                errorMessage = "🤖**Durable Functions Test Bot**🤖" + Environment.NewLine + GetTestNameHelp();
                return false;
            }
        }

        static string GetSyntaxHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"The syntax for a test with a Consumption plan is: `{CommandPrefix.Trim()} Consumption os <os type> functionsVersion <version> run <TestName> <Param1> <Param2> ... end`");
            sb.AppendLine($"Example: `{CommandPrefix.Trim()} Consumption os Windows functionsVersion 3 run HelloSequence end`");
            sb.AppendLine();
            sb.AppendLine($"The syntax for a test with an Elastic Premium plan is: `{CommandPrefix.Trim()} ElasticPremium sku <skuValue> minCount <count> maxCount <count> os <os type> functionsVersion <version> run <TestName> <Param1> <Param2> ... end`");
            sb.AppendLine($"Example: `{CommandPrefix.Trim()} ElasticPremium sku EP1 minCount 1 maxCount 3 os Windows functionsVersion 3 run HelloSequence end`");
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

        internal class GitHubRequestPayload
        {
            public string Action { get; set; }
            public IssuePayload Issue { get; set; }
            public CommentPayload Comment { get; set; }
            public object Repository { get; set; }
            public object Organization { get; set; }
            public object Enterprise { get; set; }
            public object Sender { get; set; }
        }

        internal class IssuePayload
        {
            public string Url { get; set; }
            public string Comments_Url { get; set; }
            public string Author_Association { get; set; }
            public string Number { get; set; }
            public IssuePullRequestPayload Pull_Request { get; set; }
        }

        internal class CommentPayload
        {
            public string Url { get; set; }
            public string Body { get; set; }
        }

        internal class IssuePullRequestPayload
        {
            public string Url { get; set; }
        }

    }
}