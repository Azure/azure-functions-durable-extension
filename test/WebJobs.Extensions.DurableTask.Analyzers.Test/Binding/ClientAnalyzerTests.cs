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
    public class ClientAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = ClientAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string V1ExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationClient] DurableOrchestrationClient client)
            {
            }
}";

        private const string V2ClientExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] IDurableClient client)
            {
            }
}";

        private const string V2OrchestrationClientExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] IDurableOrchestrationClient client)
            {
            }
}";

        private const string V2EntityClientExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] IDurableEntityClient client)
            {
            }
}";

        [TestMethod]
        public void DurableClient_V1_NoDiagnosticTestCases()
        {
            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(V1ExpectedFix);
        }

        [TestMethod]
        public void DurableClient_V2_NoDiagnosticTestCases()
        {
            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(V2ClientExpectedFix);
            VerifyCSharpDiagnostic(V2OrchestrationClientExpectedFix);
            VerifyCSharpDiagnostic(V2EntityClientExpectedFix);
        }

        // Tests SyntaxKind.IdentifierName
        [TestMethod]
        public void OrchestrationClient_V1_UsingObject()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationClient] Object client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix);
        }

        // Tests SyntaxKind.PredefinedType
        [TestMethod]
        public void OrchestrationClient_V1_UsingString()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationClient] string client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
            
            VerifyCSharpFix(test, V1ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        // Tests SyntaxKind.GenericName
        [TestMethod]
        public void OrchestrationClient_V1_UsingList()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationClient] List<string> client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "List<string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        // Tests SyntaxKind.ArrayType
        [TestMethod]
        public void OrchestrationClient_V1_UsingArray()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationClient] string[] client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "string[]"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        // Tests SyntaxKind.TupleType
        [TestMethod]
        public void OrchestrationClient_V1_UsingValueTuple()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationClient] (string, int) client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "(string, int)"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        // Tests Durable V2 Client
        [TestMethod]
        public void OrchestrationClient_V1_UsingV2Client()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationClient] IDurableClient client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "IDurableClient"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix);
        }

        // Tests SyntaxKind.IdentifierName
        [TestMethod]
        public void DurableClient_V2_UsingObject()
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
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] Object client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        // Tests SyntaxKind.PredefinedType
        [TestMethod]
        public void DurableClient_V2_UsingString()
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
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] string client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        // Tests SyntaxKind.GenericName
        [TestMethod]
        public void DurableClient_V2_UsingList()
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
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] List<string> client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "List<string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        // Tests SyntaxKind.ArrayType
        [TestMethod]
        public void DurableClient_V2_UsingArray()
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
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] string[] client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "string[]"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        // Tests SyntaxKind.TupleType
        [TestMethod]
        public void DurableClient_V2_UsingValueTuple()
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
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] (string, int) client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "(string, int)"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        // Tests Durable V1 Client
        [TestMethod]
        public void DurableClient_V2_V1DurableClass()
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
        [FunctionName(""ClientAnalyzerTestCases"")]
        public static async Task Run(
            [DurableClient] DurableOrchestrationClient client)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "DurableOrchestrationClient"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
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
