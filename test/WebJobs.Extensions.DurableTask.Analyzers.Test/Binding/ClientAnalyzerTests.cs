// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Binding
{
    [TestClass]
    public class ClientAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = ClientAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = ClientAnalyzer.severity;
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
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            }
}";

        [TestMethod]
        public void DurableClient_NonIssue()
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
            [DurableClient] IDurableClient durableClient, [DurableClient] IDurableOrchestrationClient durableOrchestrationClient, 
            [DurableClient] IDurableEntityClient durableEntityClient, ILogger log)
            {
            }
}";
            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DurableClient_Object()
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
            [DurableClient] Object context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V2ClientAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DurableClient_String()
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
            [DurableClient] string context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V2ClientAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DurableClient_WrongDurableInterface()
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
            [DurableClient] IDurableOrchestrationContext context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V2ClientAnalyzerMessageFormat, "IDurableOrchestrationContext"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ClientCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ClientAnalyzer();
        }
    }
}
