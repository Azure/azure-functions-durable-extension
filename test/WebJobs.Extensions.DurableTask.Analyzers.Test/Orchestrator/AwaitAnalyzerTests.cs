// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class AwaitAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = AwaitAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = AwaitAnalyzer.Severity;

        // Should not flag awaits on Durable functions.
        [TestMethod]
        public void AwaitAnalyzer_DurableContextAwait_NoDiagnosticTestCases()
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
            [FunctionName(""AwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                 await context.CallActivityAsync<string>(""HelloWorld"", ""Minneapolis"" );
            }

            [FunctionName(""HelloWorld"")]
            public static string HelloWorldActivity([ActivityTrigger] string name)
            {
                return $""Hello {name}!"";
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        // Should not flag user defined awaits that are not inside an orchestrator.
        [TestMethod]
        public void AwaitAnalyzer_NotOrchestrator_NoDiagnosticTestCases()
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
            [FunctionName(""AwaitAnalyzerTestCases"")]
            public static async Task Run()
            {
                 await UserDefinedCode();
            }

            public static Task UserDefinedCode()
            {
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }


        // should flag in orchestrator method
        [TestMethod]
        public void AwaitAnalyzer_UserDefinedCode()
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
            [FunctionName(""AwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context
            {
                 await UserDefinedCode();
            }

            public static async Task UserDefinedCode()
            {
                await UserDefinedCode();
            }
        }
    }";

            var expectedDiagnostics = new DiagnosticResult[3];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = Resources.AwaitAnalyzerMessageFormat,
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 18)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = MethodAnalyzer.DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "UserDefinedCode()"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 24)
                        }
            };

            expectedDiagnostics[2] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = Resources.AwaitAnalyzerMessageFormat,
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        // should flag when used in method directly or indirectly called using method analyzer
        // should flag in orchestrator method
        [TestMethod]
        public void AwaitAnalyzer_UserDefinedCode_NonDeterministicMethod()
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
            [FunctionName(""AwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context
            {
                DirectCall();
            }

            public static void DirectCall()
            {
                await UserDefinedCode();
            }

            public static Task UserDefinedCode()
            {
            }
        }
    }";

            var expectedDiagnostics = new DiagnosticResult[2];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = MethodAnalyzer.DiagnosticId,
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
                Message = Resources.AwaitAnalyzerMessageFormat,
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 17)
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
