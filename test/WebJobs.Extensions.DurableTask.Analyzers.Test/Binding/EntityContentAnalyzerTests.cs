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
    public class EntityContentAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = EntityContextAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string ExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""EntityContextAnalyzerTestCases"")]
        public static async Task Run(
            [EntityTrigger] IDurableEntityContext context)
            {
            }
    }
}";

        [TestMethod]
        public void EntityTrigger_NoDiagnosticTestCases()
        {
            VerifyCSharpDiagnostic(ExpectedFix);
        }

        // Tests SyntaxKind.IdentifierName
        [TestMethod]
        public void EntityTrigger_UsingObject()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""EntityContextAnalyzerTestCases"")]
        public static async Task Run(
            [EntityTrigger] Object context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests SyntaxKind.PredefinedType
        [TestMethod]
        public void EntityTrigger_UsingString()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""EntityContextAnalyzerTestCases"")]
        public static async Task Run(
            [EntityTrigger] string context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests SyntaxKind.GenericName
        [TestMethod]
        public void EntityTrigger_UsingList()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""EntityContextAnalyzerTestCases"")]
        public static async Task Run(
            [EntityTrigger] List<string> context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "List<string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        // Tests SyntaxKind.ArrayType
        [TestMethod]
        public void EntityTrigger_UsingArray()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""EntityContextAnalyzerTestCases"")]
        public static async Task Run(
            [EntityTrigger] string[] context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "string[]"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests SyntaxKind.TupleType
        [TestMethod]
        public void EntityTrigger_UsingValueTuple()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""EntityContextAnalyzerTestCases"")]
        public static async Task Run(
            [EntityTrigger] (string, int) context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "(string, int)"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
        }

        // Tests Incorrect Durable Type
        [TestMethod]
        public void EntityTrigger_UsingIncorrectDurableType()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""EntityContextAnalyzerTestCases"")]
        public static async Task Run(
            [EntityTrigger] IDurableActivityContext context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityContextAnalyzerMessageFormat, "IDurableActivityContext"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix);
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
