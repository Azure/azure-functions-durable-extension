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
        private static readonly DiagnosticSeverity Severity = ClientAnalyzer.Severity;

        private const string V1ExpectedFix = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationClient] DurableOrchestrationClient client,
            ILogger log)
            {
            }
}";

        private const string V2ClientExpectedFix = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [DurableClient] IDurableClient client,
            ILogger log)
            {
            }
}";

        private const string V2OrchestrationClientExpectedFix = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
            {
            }
}";

        private const string V2EntityClientExpectedFix = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [DurableClient] IDurableEntityClient client,
            ILogger log)
            {
            }
}";

        [TestMethod]
        public void DurableClient_V1_NonIssue()
        {
            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(V1ExpectedFix);
        }

        [TestMethod]
        public void DurableClient_V2_NonIssue()
        {
            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(V2ClientExpectedFix);
            VerifyCSharpDiagnostic(V2OrchestrationClientExpectedFix);
            VerifyCSharpDiagnostic(V2EntityClientExpectedFix);
        }

        [TestMethod]
        public void OrchestrationClient_V1_Object()
        {
            var test = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationClient] Object client,
            ILogger log)
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
                            new DiagnosticResultLocation("Test0.cs", 11, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix);
        }

        [TestMethod]
        public void OrchestrationClient_V1_String()
        {
            var test = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationClient] string client,
            ILogger log)
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
                            new DiagnosticResultLocation("Test0.cs", 11, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
            
            VerifyCSharpFix(test, V1ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void OrchestrationClient_V1_V2DurableInterface()
        {
            var test = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationClient] IDurableClient client,
            ILogger log)
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
                            new DiagnosticResultLocation("Test0.cs", 11, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix);
        }

        [TestMethod]
        public void OrchestrationClient_V1_Tuple()
        {
            var test = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationClient] Tuple<int, string> client,
            ILogger log)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "Tuple<int, string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix);
        }

        [TestMethod]
        public void DurableClient_V2_Object()
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
            [DurableClient] Object client,
            ILogger log)
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
                            new DiagnosticResultLocation("Test0.cs", 11, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        [TestMethod]
        public void DurableClient_V2_String()
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
            [DurableClient] string client,
            ILogger log)
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
                            new DiagnosticResultLocation("Test0.cs", 11, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        [TestMethod]
        public void DurableClient_V2_V1DurableClass()
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
            [DurableClient] DurableOrchestrationClient client,
            ILogger log)
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
                            new DiagnosticResultLocation("Test0.cs", 11, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ClientExpectedFix, 0);
            VerifyCSharpFix(test, V2EntityClientExpectedFix, 1);
            VerifyCSharpFix(test, V2OrchestrationClientExpectedFix, 2);
        }

        [TestMethod]
        public void DurableClient_V2_Tuple()
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
            [DurableClient] Tuple<int, string> client,
            ILogger log)
            {
            }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "Tuple<int, string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 29)
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
