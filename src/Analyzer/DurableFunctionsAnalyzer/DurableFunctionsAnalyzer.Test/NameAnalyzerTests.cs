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
    public class NameAnalyzerTests : CodeFixVerifier
    {

        [TestMethod]
        public void Should_not_trigger_on_empty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }
        [TestMethod]
        public void Should_not_find_any_issue_with_correctly_named_functions()
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
                var approvals = await context.CallActivityAsync<string>(""ApplicationsFiltered"", ""An approval"");
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<string> Run(
            [ActivityTrigger(""approval-queue"")] String approval,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";
            

            VerifyCSharpDiagnostic(test);

            //        var fixtest = @"
            //using System;
            //using System.Collections.Generic;
            //using System.Linq;
            //using System.Text;
            //using System.Threading.Tasks;
            //using System.Diagnostics;

            //namespace ConsoleApplication1
            //{
            //    class TYPENAME
            //    {   
            //    }
            //}";
            //        VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void Should_not_find_any_issue_with_name_of()
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
                var approvals = await context.CallActivityAsync<List<Application>>(nameof(ApplicationsFiltered));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(nameof(ApplicationsFiltered))]
        public static async Task ApplicationsFiltered(
            [QueueTrigger(""approval-queue"")] Approval approval,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";


            VerifyCSharpDiagnostic(test);

            //        var fixtest = @"
            //using System;
            //using System.Collections.Generic;
            //using System.Linq;
            //using System.Text;
            //using System.Threading.Tasks;
            //using System.Diagnostics;

            //namespace ConsoleApplication1
            //{
            //    class TYPENAME
            //    {   
            //    }
            //}";
            //        VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void Should_find_incorrectly_named_function()
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
                var approvals = await context.CallActivityAsync<List<Application>>(""ApplicationsFiltered"");
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFilteredNicely"")]
        public static async Task Run(
            [QueueTrigger(""approval-queue"")] Approval approval,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "DurableFunctionsNameAnalyzer",
                Message = String.Format("Azure function named '{0}' does not exist. Did you mean 'ApplicationsFilteredNicely'?", "ApplicationsFiltered"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 84)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //        var fixtest = @"
            //using System;
            //using System.Collections.Generic;
            //using System.Linq;
            //using System.Text;
            //using System.Threading.Tasks;
            //using System.Diagnostics;

            //namespace ConsoleApplication1
            //{
            //    class TYPENAME
            //    {   
            //    }
            //}";
            //        VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void Should_find_incorrectly_named_function_using_retry_policy()
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
                var retryOptions = new RetryOptions(TimeSpan.FromSeconds(1), 3);
                var applications = context.GetInput<List<Application>>();
                var approvals = await context.CallActivityWithRetryAsync<List<Application>>(""ApplicationsFiltered"", retryOptions, ""approved"");
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFilteredNicely"")]
        public static async Task Run(
            [ActivityTrigger(""approval-queue"")] String approval,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "DurableFunctionsNameAnalyzer",
                Message = String.Format("Azure function named '{0}' does not exist. Did you mean 'ApplicationsFilteredNicely'?", "ApplicationsFiltered"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 22, 93)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //        var fixtest = @"
            //using System;
            //using System.Collections.Generic;
            //using System.Linq;
            //using System.Text;
            //using System.Threading.Tasks;
            //using System.Diagnostics;

            //namespace ConsoleApplication1
            //{
            //    class TYPENAME
            //    {   
            //    }
            //}";
            //        VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void Should_find_when_no_function_has_been_declared()
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
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
                var applications = context.GetInput<List<Application>>();
                var approvals = await context.CallActivityAsync<List<Application>>(""ApplicationsFiltered"");
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        public static async Task Run(
            [QueueTrigger(""approval-queue"")] Approval approval,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "DurableFunctionsNameAnalyzer",
                Message = String.Format("Azure function named '{0}' does not exist. Could not find any function registrations.", "ApplicationsFiltered"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 84)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //        var fixtest = @"
            //using System;
            //using System.Collections.Generic;
            //using System.Linq;
            //using System.Text;
            //using System.Threading.Tasks;
            //using System.Diagnostics;

            //namespace ConsoleApplication1
            //{
            //    class TYPENAME
            //    {   
            //    }
            //}";
            //        VerifyCSharpFix(test, fixtest);
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
