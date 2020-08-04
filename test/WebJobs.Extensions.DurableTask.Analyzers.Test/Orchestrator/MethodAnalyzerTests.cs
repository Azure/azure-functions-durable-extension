// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class MethodAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = MethodAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void MethodCalls_NoDiagnosticTestCases()
        {
            var test = @"
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MethodAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                string.ToLower(""Testing method call not defined in source code"");
                DirectCall();
            }

        public static void DirectCall()
        {
            string.ToUpper(""Method not defined in source code"");
            IndirectCall();
            RecursiveCall();
        }

        public static void IndirectCall()
        {
        }

        public static void RecursiveCall()
        {
            RecursiveCall();
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void MethodCalls_DirectCall()
        {
            var test = @"
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MethodAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                DirectCall();
            }

        public static void DirectCall()
        {
            var dateTime = DateTime.Now;
        }
    }
}";

            var expectedDiagnostics = new DiagnosticResult[2];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "DirectCall()"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DateTimeAnalyzer.DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Now"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 28)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void MethodCalls_IndirectCall()
        {
            var test = @"
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MethodAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                DirectCall();
            }

        public static void DirectCall()
        {
            IndirectCall();
        }

        public static void IndirectCall()
        {
            var dateTime = DateTime.Now;
        }
    }
}";

            var expectedDiagnostics = new DiagnosticResult[3];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "DirectCall()"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "IndirectCall()"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 13)
                        }
            };

            expectedDiagnostics[2] = new DiagnosticResult
            {
                Id = DateTimeAnalyzer.DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Now"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 25, 28)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void MethodCalls_NonShortCircuit()
        {
            var test = @"
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MethodAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                DirectCall();
            }

        public static void DirectCall()
        {
            var dateTime = DateTime.Now;
            Environment.GetEnvironmentVariable(""test"");
        }
    }
}";

            var expectedDiagnostics = new DiagnosticResult[3];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "DirectCall()"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DateTimeAnalyzer.DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Now"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 28)
                        }
            };

            expectedDiagnostics[2] = new DiagnosticResult
            {
                Id = EnvironmentVariableAnalyzer.DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Environment.GetEnvironmentVariable"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OrchestratorAnalyzer();
        }
    }
}
