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
    public class OrchestratorContextAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = OrchestratorContextAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = OrchestratorContextAnalyzer.Severity;

        private const string V1ExpectedFix = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
               
            }
    }
}";

        private const string V1BaseExpectedFix = @"
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContextBase context,
            ILogger log)
            {
               
            }
    }
}";

        private const string V2ExpectedFix = @"
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
    }
}";

        [TestMethod]
        public void OrchestrationContext_V1_NonIssue()
        {
            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(V1ExpectedFix);
            VerifyCSharpDiagnostic(V1BaseExpectedFix);
        }

        [TestMethod]
        public void OrchestrationContext_V2_NonIssue()
        {
            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(V2ExpectedFix);
        }


        [TestMethod]
        public void OrchestrationContext_V1_Object()
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
            [OrchestrationTrigger] Object context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        [TestMethod]
        public void OrchestrationContext_V1_String()
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
            [OrchestrationTrigger] string context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        [TestMethod]
        public void OrchestrationContext_V1_Tuple()
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
            [OrchestrationTrigger] Tuple<int, string> context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "Tuple<int, string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        [TestMethod]
        public void OrchestrationContext_V1_V2DurableInterface()
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
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "IDurableOrchestrationContext"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        [TestMethod]
        public void OrchestrationContext_V2_Object()
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
            [OrchestrationTrigger] Object context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        [TestMethod]
        public void OrchestrationContext_V2_String()
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
            [OrchestrationTrigger] string context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        [TestMethod]
        public void OrchestrationContext_V2_Tuple()
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
            [OrchestrationTrigger] Tuple<int, string> context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "Tuple<int, string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        [TestMethod]
        public void OrchestrationContext_V2_V1DurableClass()
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
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
               
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "DurableOrchestrationContext"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new OrchestratorContextCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OrchestratorContextAnalyzer();
        }
    }
}
