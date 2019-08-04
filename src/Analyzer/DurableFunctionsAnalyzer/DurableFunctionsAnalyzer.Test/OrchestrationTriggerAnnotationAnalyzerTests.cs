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
    public class OrchestrationTriggerAnnotationAnalyzerTests : CodeFixVerifier
    {

        [TestMethod]
        public void Should_not_trigger_on_empty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Should_find_issue_with_object()
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
            [OrchestrationTrigger] Object context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = "DurableFunctionOrchestrationTriggerAnalyzer",
                Message = String.Format("OrchestrationTrigger must be attached to a DurableOrchestrationContext", "ApplicationsFiltered", "System.Threading.Tasks.Task<System.Collections.Generic.List<System.String>>", "System.Threading.Tasks.Task<System.Collections.Generic.List<System.Int32>>"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 14)
                     }
            };
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Should_find_any_issue_with_key_word()
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
            [OrchestrationTrigger] string context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = "DurableFunctionOrchestrationTriggerAnalyzer",
                Message = String.Format("OrchestrationTrigger must be attached to a DurableOrchestrationContext", "ApplicationsFiltered", "System.Threading.Tasks.Task<System.Collections.Generic.List<System.String>>", "System.Threading.Tasks.Task<System.Collections.Generic.List<System.Int32>>"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 14)
                     }
            };
            VerifyCSharpDiagnostic(test, expected);
        }


        [TestMethod]
        public void Should_not_find_any_issue_with_orchestration_trigger_on_context()
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
               
            }
}";
            
            VerifyCSharpDiagnostic(test);
        }


        [TestMethod]
        public void Should_not_find_any_issue_with_orchestration_trigger_on_context_base()
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
            [OrchestrationTrigger] DurableOrchestrationContextBase context,
            ILogger log)
            {
               
            }
}";

            VerifyCSharpDiagnostic(test);
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
