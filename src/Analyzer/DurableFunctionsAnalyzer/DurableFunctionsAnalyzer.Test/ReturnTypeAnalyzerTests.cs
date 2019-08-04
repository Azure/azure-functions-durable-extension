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
    public class ReturnTypeAnalyzerTests : CodeFixVerifier
    {

        [TestMethod]
        public void Should_not_trigger_on_empty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }
        [TestMethod]
        public void Should_not_find_any_issue_with_void_return()
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
                var approvals = await context.CallActivityAsync(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static void Run(
            [ActivityTrigger] String userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void Should_not_find_any_issue_with_untyped_task_return()
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
                var approvals = await context.CallActivityAsync(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task Run(
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
        public void Should_not_find_any_issue_with_string_keyword()
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
                var approvals = await context.CallActivityAsync<string>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<string> Run(
            [ActivityTrigger] string userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void Should_not_find_any_issue_with_different_long_representations()
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
                var approvals = await context.CallActivityAsync<List<long>>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<Int64>> Run(
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
        public void Should_not_find_any_issue_with_int_representations()
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
                var approvals = await context.CallActivityAsync<List<Int>>(""ApplicationsFiltered"", new String(""));
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

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void Should_not_find_any_issue_with_double_representations()
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
                var approvals = await context.CallActivityAsync<List<double>>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<Double>> Run(
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
        public void Should_not_find_any_issue_with_activity_not_being_async()
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
                var approvals = await context.CallActivityAsync<string>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static string Run(
            [ActivityTrigger] String userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";

            VerifyCSharpDiagnostic(test);

        }

        [TestMethod]
        public void Should_not_find_any_issue_with_different_bool_representations()
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
                var approvals = await context.CallActivityAsync<List<bool>>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<Boolean>> Run(
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
        public void Should_not_find_any_issue_with_matching_return_type()
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
                var approvals = await context.CallActivityAsync<List<Application>>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.OrderByDescending(x => x.Score).First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<Application>> Run(
            [ActivityTrigger] string userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Should_find_issue_with_non_matching_return_type()
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
                var approvals = await context.CallActivityAsync<List<int>>(""ApplicationsFiltered"", new String(""));
                log.LogInformation($""Approval received. {approvals.Count} applicants approved"");
                return approvals.First();
            }

        [FunctionName(""ApplicationsFiltered"")]
        public static async Task<List<string>> Run(
            [ActivityTrigger] string userName,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(approval.InstanceId, ""ApplicationsFiltered"", approval.Applications);
        }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = "DurableFunctionsReturnTypeAnalyzer",
                Message = String.Format("Azure function named '{0}' returns '{1}' but '{2}' is expected", "ApplicationsFiltered", "System.Threading.Tasks.Task<System.Collections.Generic.List<System.String>>", "System.Threading.Tasks.Task<System.Collections.Generic.List<System.Int32>>"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                   new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 39)
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
