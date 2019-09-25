// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    //[TestClass]
    public class OrchestratorContextAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = OrchestratorContextAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = OrchestratorContextAnalyzer.severity;
        private readonly string fixtestV2 = @"
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
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";

        [TestMethod]
        public void V2_DurableInterface()
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
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";
            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void V2_NonDurableInterface_Object()
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
                Id = diagnosticId,
                Message = String.Format("OrchestrationTrigger is attached to a '{0}' but must be attached to an IDurableOrchestrationContext instead", "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void V2_NonDurableInterface_String()
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
                Id = diagnosticId,
                Message = String.Format("OrchestrationTrigger is attached to a '{0}' but must be attached to an IDurableOrchestrationContext instead", "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void V2_V1DurableClass()
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
                Id = diagnosticId,
                Message = String.Format("OrchestrationTrigger is attached to a '{0}' but must be attached to an IDurableOrchestrationContext instead", "DurableOrchestrationContext"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }
        
        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new OrchestratorContextCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OrchestratorContextAnalyzer();
        }
    }
}
