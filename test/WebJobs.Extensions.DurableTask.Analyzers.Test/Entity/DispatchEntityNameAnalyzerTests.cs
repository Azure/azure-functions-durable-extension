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
        private readonly string diagnosticId = DispatchClassNameAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = DispatchClassNameAnalyzer.severity;

        private readonly string fix = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<MyEmptyEntity>();
    }";

        [TestMethod]
        public void DispatchCall_NonIssue()
        {
            VerifyCSharpDiagnostic(fix);
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
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DispatchClassNameAnalyzerMessageFormat, "Object", "MyEmptyEntity"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix);
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
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DispatchClassNameAnalyzerMessageFormat, "string", "MyEmptyEntity"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix);
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
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DispatchClassNameAnalyzerMessageFormat, "ILogger", "MyEmptyEntity"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix);
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
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DispatchClassNameAnalyzerMessageFormat, "Tuple<int, string>", "MyEmptyEntity"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new DispatchClassNameCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DispatchClassNameAnalyzer();
        }
    }
}
