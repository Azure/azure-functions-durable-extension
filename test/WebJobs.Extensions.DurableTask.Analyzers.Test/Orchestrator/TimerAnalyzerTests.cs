// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class TimerAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = TimerAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = TimerAnalyzer.Severity;

        private readonly string allTests = @"
            public void timerAllCalls()
            {
                Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Task.Delay(100);
                System.Threading.Tasks.Task.Delay(100);
            }
}
    }";

        [TestMethod]
        public void TimerInMethod_NonIssueCalls()
        {
            var test = @"
    using System;

    namespace VSSample
    {
        public static class TimerNonIssueCalls
        {
            " + allTests;

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TaskInOrchestrator_Delay_Namespace()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                System.Threading.Tasks.Task.Delay(100);
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Delay(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TaskInOrchestrator_Delay()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Task.Delay(100);
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Delay(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ThreadInOrchestrator_Sleep_Namespace()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Thread.Sleep(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ThreadInOrchestrator_Sleep()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Thread.Sleep(100);
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Thread.Sleep(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Timer_DeterministicAttribute_All()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""E1_HelloSequence"")]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                DirectCall();
            
                return ""Hello"";
            }

        public static string DirectCall()
        {
            " + allTests;


            var expectedResults = new DiagnosticResult[5];
            expectedResults[0] = new DiagnosticResult
            {
                Id = MethodAnalyzer.DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "DirectCall()"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 17)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Thread.Sleep(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 27, 17)
                        }
            };

            expectedResults[2] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Thread.Sleep(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 28, 17)
                        }
            };

            expectedResults[3] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Delay(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 29, 17)
                        }
            };

            expectedResults[4] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Delay(100)"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 30, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new TimerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OrchestratorAnalyzer();
        }
    }
}
