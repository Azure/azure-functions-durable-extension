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
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string ExpectedFix = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<MyEmptyEntity>();
    }";

        [TestMethod]
        public void DispatchCall_NoDiagnosticTestCases()
        {
            VerifyCSharpDiagnostic(ExpectedFix);
        }

        // Tests SyntaxKind.IdentifierName
        [TestMethod]
        public void DispatchCall_UsingObject()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
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
                            new DiagnosticResultLocation("Test0.cs", 9, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests SyntaxKind.PredefinedType
        [TestMethod]
        public void DispatchCall_UsingString()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
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
                            new DiagnosticResultLocation("Test0.cs", 9, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests SyntaxKind.GenericName
        [TestMethod]
        public void DispatchCall_UsingList()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<List<string>>();
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DispatchEntityNameAnalyzerMessageFormat, "List<string>", "MyEmptyEntity"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 9, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests SyntaxKind.ArrayType
        [TestMethod]
        public void DispatchCall_UsingArray()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<string[]>();
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DispatchEntityNameAnalyzerMessageFormat, "string[]", "MyEmptyEntity"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 9, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests SyntaxKind.TupleType
        [TestMethod]
        public void DispatchCall_UsingValueTuple()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

 public class MyEmptyEntity : IMyEmptyEntity
    {
        [FunctionName(""MyEmptyEntity"")]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<(string, int)>();
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DispatchEntityNameAnalyzerMessageFormat, "(string, int)", "MyEmptyEntity"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 9, 96)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests interface not defined in user code
        [TestMethod]
        public void DispatchCall_ILogger()
        {
            var test = @"
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
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
                            new DiagnosticResultLocation("Test0.cs", 9, 96)
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
