// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableFunctionsAnalyzer.analyzers;
using DurableFunctionsAnalyzer.analyzers.orchestrator;
using DurableFunctionsAnalyzer.codefixproviders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;

namespace DurableFunctionsAnalyzer.Test
{
    //[TestClass]
    public class TimerAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = TimerAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = TimerAnalyzer.severity;

        String fixtest = @"
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
                context.CreateTimer;
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
            public void timerNonIssueCalls()
            {
			    Thread.Sleep(100);
                Task.Delay(100);
            }
        }
    }";

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
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "System.Threading.Tasks.Task.Delay"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
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
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "Task.Delay"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
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
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "Thread.Sleep"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
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
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "Thread.Sleep"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }
        

        [TestMethod]
        public void TaskInMethod_DeterministicAttribute_Delay()
        {
            var test = @"
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        [Deterministic]
        public int testDeterministicMethod()
        {
            Task.Delay(100);
            return 5;
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "Task.Delay"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            // Test fix
        }

        [TestMethod]
        public void ThreadInMethod_DeterministicAttribute_Sleep()
        {
            var test = @"
    using System;
    using System.Threading;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        [Deterministic]
        public int testDeterministicMethod()
        {
            Thread.Sleep(100);
            return 5;
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "Thread.Sleep"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            // Test fix
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new TimerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new TimerAnalyzer();
        }
    }
}
