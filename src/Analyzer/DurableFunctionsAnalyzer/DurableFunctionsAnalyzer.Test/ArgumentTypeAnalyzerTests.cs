using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using DurableFunctionsAnalyzer;

namespace DurableFunctionsAnalyzer.Test
{
    [TestClass]
    public class ArgumentAnalyzerTests : CodeFixVerifier
    {

        [TestMethod]
        public void Should_not_trigger_on_empty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Should_not_find_any_issue_with_tuple_parameter()
        {
            var test = @"using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
                var applications = context.GetInput<List<Application>>();
                var approvals = await context.CallActivityAsync<List<String>>(""ApplicationsFiltered"", (new String(""a string""), 1));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<string>> Run(
            [ActivityTrigger] (String userName, int Length),
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Should_not_find_any_issue_with_correctly_function_parameter()
        {
            var test = @"using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
                var applications = context.GetInput<List<Application>>();
                var approvals = await context.CallActivityAsync<List<String>>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<string>> Run(
            [ActivityTrigger] String userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";
            
            VerifyCSharpDiagnostic(test); 
        }


        [TestMethod]
        public void Should_not_find_any_issue_with_open_generic_function_parameter()
        {
            var test = @"using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public class EatIceCream
    {
        public string Flavour { get; set; }
    }

    public class Command<T>
    {
        public T WhatToEat { get; set; }
        public Command()
        {
            
        }
    }
    public static class HireEmployee
    {
        [FunctionName(""ConsumeDessert"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
                var applications = context.GetInput<List<Application>>();
                var approvals = await context.CallActivityAsync<List<String>>(""Consume"", new Command<EatIceCream>());
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""Consume"")]
        public static async Task<List<string>> Run(
            [ActivityTrigger] Command<EatIceCream> command,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""AllIceCreamEaten"");
        }
    }
}";

            VerifyCSharpDiagnostic(test);

        }


        [TestMethod]
        public void Should_not_crash_on_real_null_argument()
        {
            var test = @"using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Timeout
{
    public static class RunTransaction
    {
        [FunctionName(""RunTransaction"")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger logger)
        {
            var outputs = new List<string>();
            using (var cts = new CancellationTokenSource())
            {
                var timer = context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(15), cts.Token);
                var action = context.CallActivityAsync<string>(""SomethingTimeConsuming"", null);

                var result = await Task<string>.WhenAny(timer, action);
                if (result == action)
                {
                    cts.Cancel();
                    logger.LogInformation(""Approved"");
                }
                else
                {
                    logger.LogInformation(""Task timed out"");
                }
            }
            return outputs;
        }

        [FunctionName(""Timeout"")]
        public static void Timeout([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($""Request for approval timed out."");
        }

        [FunctionName(""SomethingTimeConsuming"")]
        public async static Task<string> SomethingTimeConsuming([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($""Performing lengthy task."");
            await Task.Delay(TimeSpan.FromSeconds(30));
            return $""Approved"";
        }

        [FunctionName(""RunTransaction_HttpStart"")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, ""get"", ""post"")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(""RunTransaction"", null);

            log.LogInformation($""Started orchestration with ID = '{instanceId}'."");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Should_not_crash_on_null_argument()
        {
            var test = @"using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
                var applications = context.GetInput<List<Application>>();
                var approvals = await context.CallActivityAsync<List<String>>(""ApplicationsFiltered"", null);
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<string>> Run(
            [ActivityTrigger] String userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"",  approval.Applications);
        }
    }
}";

            VerifyCSharpDiagnostic(test);

        }


        [TestMethod]
        public void Should_find_issue_with_incorrect_function_parameter()
        {
            var test = @"using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
                var applications = context.GetInput<List<Application>>();
                var approvals = await context.CallActivityAsync<List<int>>(""ApplicationsFiltered"", Guid.NewGuid());
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<int>> Run(
            [ActivityTrigger] String userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "DurableFunctionsArgumentAnalyzer",
                Message = String.Format("Azure function named '{0}' takes a '{1}' but was given a '{2}'", "ApplicationsFiltered", "System.String", "System.Guid"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 100)
                        }
            };

            VerifyCSharpDiagnostic(test, expected); 
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new DurableFunctionsAnalyzerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AnalyzerRegistration();
        }
    }
}
