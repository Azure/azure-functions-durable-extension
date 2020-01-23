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
    public class DispatchEntityNameAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = DispatchEntityNameAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DispatchEntityNameAnalyzer.Severity;

        private const string ExpectedFix = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<MyEmptyEntity>();
    }";

        [TestMethod]
        public void DispatchCall_NonIssue()
        {
            VerifyCSharpDiagnostic(ExpectedFix);
        }

        [TestMethod]
        public void DispatchCall_Object()
        {
            var test = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<Object>();
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DispatchEntityNameAnalyzerMessageFormat, "Object", "MyEmptyEntity"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        [TestMethod]
        public void DispatchCall_String()
        {
            var test = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<string>();
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DispatchEntityNameAnalyzerMessageFormat, "string", "MyEmptyEntity"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        [TestMethod]
        public void DispatchCall_ILogger()
        {
            var test = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<ILogger>();
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DispatchEntityNameAnalyzerMessageFormat, "ILogger", "MyEmptyEntity"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        [TestMethod]
        public void DispatchCall_Tuple()
        {
            var test = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<Tuple<int, string>>();
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DispatchEntityNameAnalyzerMessageFormat, "Tuple<int, string>", "MyEmptyEntity"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new DispatchEntityNameCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DispatchEntityNameAnalyzer();
        }
    }
}
