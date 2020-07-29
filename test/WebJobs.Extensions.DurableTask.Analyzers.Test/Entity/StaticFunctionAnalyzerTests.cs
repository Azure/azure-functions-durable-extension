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
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string ExpectedFix = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<MyEmptyEntity>();
    }";

        [TestMethod]
        public void StaticFunction_NoDiagnosticTestCases()
        {
            VerifyCSharpDiagnostic(ExpectedFix);
        }

        [TestMethod]
        public void StaticFunction_NotStatic()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<MyEmptyEntity>();
    }";

            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityStaticAnalyzerMessageFormat, "Run"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 8, 21)
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
            return new DispatchEntityNameAnalyzer();
        }
    }
}
