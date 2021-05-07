using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DFTestBotRoutingApp
{
    public static class RoutingFunction
    {
        private const string prodCommandPrefix = "/DFTest";
        private const string stagingCommandPrefix = "/DFStagingTest";

        [FunctionName("CallTestBot")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger logger)
        {
            GitHubRequestPayload gitHubRequestPayload = context.GetInput<GitHubRequestPayload>();

            // get command from json (e.g. "/DFTest")
            string commentBody = gitHubRequestPayload.Comment.Body;
            bool startsWithDFTest = commentBody.StartsWith(prodCommandPrefix);

            string targetUrl = await context.CallActivityAsync<string>("GetTestBotUrl", startsWithDFTest);

            Dictionary<string, StringValues> header = new Dictionary<string, StringValues>
            {
                { "Content-Type", "application/json" }
            };

            DurableHttpRequest durableHttpRequest = new DurableHttpRequest(HttpMethod.Post, new Uri(targetUrl), header, JsonConvert.SerializeObject(gitHubRequestPayload));
            await context.CallHttpAsync(durableHttpRequest);
        }

        [FunctionName("GetTestBotUrl")]
        public static string GetTestBotUrl([ActivityTrigger] bool startsWithDFTest, ILogger log)
        {
            string prodFunctionKey = Environment.GetEnvironmentVariable("PROD_TEST_BOT_FUNCTION_KEY");
            string stagingFunctionKey = Environment.GetEnvironmentVariable("STAGING_TEST_BOT_FUNCTION_KEY");

            string targetUrl = startsWithDFTest
                ? $"https://dftestbotapp.azurewebsites.net/api/GitHubComment?code={prodFunctionKey}"
                : $"https://dftestbotapp-staging.azurewebsites.net/api/GitHubComment?code={stagingFunctionKey}";
            
            return targetUrl;
        }

        [FunctionName("Routing_HttpStart")]
        public static async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Validate that the HttpRequest Content is JSON
            if (!req.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult("Expected application/json");
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
                    return new BadRequestObjectResult($"Invalid JSON: {e.Message}");
                }
            }

            string action = gitHubRequestPayload.Action;
            IssuePullRequestPayload issuePullRequestPayload = gitHubRequestPayload.Issue.Pull_Request;

            // checks if the issue is a pull request
            if (issuePullRequestPayload == null)
            {
                return new BadRequestObjectResult("Not a pull request comment");
            }
            // check if the issue comment is new or edited
            else if (!action.Equals("created") && !action.Equals("edited"))
            {
                return new BadRequestObjectResult($"Not a new/edited comment (action = '{action}");
            }

            // Get the test command from the body of the issue comment
            string commentBody = gitHubRequestPayload.Comment.Body;

            // Check whether comment starts with "/DFTest" or "/DFStagingTest"
            bool startsWithDFTest = commentBody.StartsWith(prodCommandPrefix, StringComparison.OrdinalIgnoreCase);
            bool startsWithDFTestStaging = commentBody.StartsWith(stagingCommandPrefix, StringComparison.OrdinalIgnoreCase);

            // Return if comment body doesn't start with "/DFTest" or "DFStagingTest"
            // and check if comment body contains "Durable Functions Test Bot" which shows up
            // in a help message for example.
            if ((!startsWithDFTest && !startsWithDFTestStaging) || commentBody.Contains("Durable Functions Test Bot"))
            {
                return new OkObjectResult("No commands detected");
            }

            // Start the orchestrator function to call the prod or staging app
            string instanceId = await starter.StartNewAsync("CallTestBot", gitHubRequestPayload);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            string scheduledMessage = startsWithDFTest ? $"Scheduled Test Bot execution in production environment." : $"Scheduled Test Bot execution in staging environment.";
            return new OkObjectResult(scheduledMessage);
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