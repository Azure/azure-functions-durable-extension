// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Entity
{
    [TestClass]
    public class StaticFunctionAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = StaticFunctionAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = StaticFunctionAnalyzer.Severity;

        private readonly string fix = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static void RunOrchestrator(
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            } 
    }
}";

        [TestMethod]
        public void StaticFunction_NonIssue()
        {
            VerifyCSharpDiagnostic(fix);
        }

        [TestMethod]
        public void StaticFunction_NonStatic()
        {
            var test = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public void RunOrchestrator(
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            } 
    }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityStaticAnalyzerMessageFormat, "RunOrchestrator"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 21)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix, allowNewCompilerDiagnostics: true);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new StaticFunctionCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new StaticFunctionAnalyzer();
        }
    }
}
