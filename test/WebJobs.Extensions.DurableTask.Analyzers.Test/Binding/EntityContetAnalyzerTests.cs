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
    public class EntityContetAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = EntityContextAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = EntityContextAnalyzer.severity;
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
        public void EntityTrigger_NonIssue()
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
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void EntityTrigger_Object()
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
            [EntityTrigger] Object context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.EntityContextAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void EntityTrigger_String()
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
            [EntityTrigger] string context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.EntityContextAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void EntityTrigger_WrongDurableInterface()
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
            [EntityTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.EntityContextAnalyzerMessageFormat, "IDurableOrchestrationContext"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new EntityContextCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EntityContextAnalyzer();
        }
    }
}
