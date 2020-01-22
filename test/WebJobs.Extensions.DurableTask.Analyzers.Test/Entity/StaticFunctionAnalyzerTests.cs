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
        private static readonly string DiagnosticId = StaticFunctionAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = StaticFunctionAnalyzer.Severity;

        private const string ExpectedFix = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static void EntityAnalyzerTest(
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            } 
    }
}";

        [TestMethod]
        public void StaticFunction_NonIssue()
        {
            VerifyCSharpDiagnostic(ExpectedFix);
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
        public void EntityAnalyzerTest(
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            } 
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityStaticAnalyzerMessageFormat, "RunOrchestrator"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 21)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
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
