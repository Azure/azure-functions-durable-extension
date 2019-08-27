using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using DurableFunctionsAnalyzer.codefixproviders;
using DurableFunctionsAnalyzer.analyzers;

namespace DurableFunctionsAnalyzer.Test
{
    [TestClass]
    public class TriggerContextAnalyzerTests : CodeFixVerifier
    {

        [TestMethod]
        public void Context_DurableInterface()
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
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Context_NonDurableInterface_Object()
        {
            var test = @"
using System.Collections.Generic;
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
                Id = "TriggerContextAnalyzer",
                Message = String.Format("OrchestrationTrigger is attached to a '{0}' but must be attached to an IDurableOrchestrationContext instead", "Object"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Context_NonDurableInterface_String()
        {
            var test = @"
using System.Collections.Generic;
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
                Id = "TriggerContextAnalyzer",
                Message = String.Format("OrchestrationTrigger is attached to a '{0}' but must be attached to an IDurableOrchestrationContext instead", "string"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Context_OldDurableClass()
        {
            var test = @"
using System.Collections.Generic;
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
            var expected = new DiagnosticResult
            {
                Id = "TriggerContextAnalyzer",
                Message = String.Format("OrchestrationTrigger is attached to a '{0}' but must be attached to an IDurableOrchestrationContext instead", "DurableOrchestrationContext"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);
        }
        
        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new TriggerContextCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new TriggerContextAnalyzer();
        }
    }
}
