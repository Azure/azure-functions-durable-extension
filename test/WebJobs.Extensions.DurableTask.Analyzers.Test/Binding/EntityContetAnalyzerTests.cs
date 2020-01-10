// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Binding
{
    [TestClass]
    public class EntityContetAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = EntityContextAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = EntityContextAnalyzer.Severity;

        private readonly string fix = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
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
    }
}";

        [TestMethod]
        public void EntityTrigger_NonIssue()
        {
            VerifyCSharpDiagnostic(fix);
        }

        [TestMethod]
        public void EntityTrigger_Object()
        {
            var test = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
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
    }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 29)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix);
        }

        [TestMethod]
        public void EntityTrigger_String()
        {
            var test = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
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
    }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 29)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix);
        }

        [TestMethod]
        public void EntityTrigger_Tuple()
        {
            var test = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [EntityTrigger] Tuple<int, string> context,
            ILogger log)
            {
            }
    }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "Tuple<int, string>"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 29)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix);
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
