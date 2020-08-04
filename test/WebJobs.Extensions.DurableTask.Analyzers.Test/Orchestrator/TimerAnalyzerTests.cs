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
        private static readonly string DiagnosticId = TimerAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string allTests = @"
            public void timerAllCalls()
            {
                Thread.Sleep(100);
                System.Threading.Thread.Sleep(100);
                Task.Delay(100);
                System.Threading.Tasks.Task.Delay(100);
            }
}
    }";

        private const string ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
            await context.CreateTimer(context.CurrentUtcDateTime.AddMilliseconds(100), CancellationToken.None);
            }
        }
    }";

        [TestMethod]
        public void Timer_NoDiagnosticTestCases()
        {
            var test = @"
    using System;

    namespace VSSample
    {
        public static class TimerNonIssueCalls
        {
            [FunctionName(""E1_HelloSequence"")]
            " + allTests;

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Task_Delay_WithNamespace()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                System.Threading.Tasks.Task.Delay(100);
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Delay(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void Task_Delay()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Task.Delay(100);
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Delay(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void Thread_Sleep_WithNamespace()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Thread.Sleep(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void Thread_Sleep()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Thread.Sleep(100);
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Thread.Sleep(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void Timer_NonDeterministicMethod_AllTimerCases()
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
            }

        public static string DirectCall()
        {
            " + allTests;


            var expectedDiagnostics = new DiagnosticResult[5];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = MethodAnalyzer.DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "DirectCall()"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 17)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Thread.Sleep(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 25, 17)
                        }
            };

            expectedDiagnostics[2] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Thread.Sleep(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 26, 17)
                        }
            };

            expectedDiagnostics[3] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Delay(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 27, 17)
                        }
            };

            expectedDiagnostics[4] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Delay(100)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 28, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void CodeFixProvider_Await_New()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await Task.Delay(new TimeSpan(100), new CancellationToken());
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
            await context.CreateTimer(context.CurrentUtcDateTime.Add(new TimeSpan(100)), new CancellationToken());
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void CodeFixProvider_Await_Variables()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                var cancellationToken = new CancellationToken();
                await Task.Delay(timeSpan, cancellationToken);
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                var cancellationToken = new CancellationToken();
            await context.CreateTimer(context.CurrentUtcDateTime.Add(timeSpan), cancellationToken);
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void CodeEFixProvider_Await_MemberAccessExpression()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                await Task.Delay(timeSpan, CancellationToken.None);
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
            await context.CreateTimer(context.CurrentUtcDateTime.Add(timeSpan), CancellationToken.None);
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void CodeFixProvider_Await_TimeSpanOnly()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                await Task.Delay(timeSpan);
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
            await context.CreateTimer(context.CurrentUtcDateTime.Add(timeSpan), CancellationToken.None);
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void CodeFixProvider_New()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Task.Delay(new TimeSpan(100), new CancellationToken());
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
            await context.CreateTimer(context.CurrentUtcDateTime.Add(new TimeSpan(100)), new CancellationToken());
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void CodeFixProvider_Variables()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                var cancellationToken = new CancellationToken();
                Task.Delay(timeSpan, cancellationToken);
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                var cancellationToken = new CancellationToken();
            await context.CreateTimer(context.CurrentUtcDateTime.Add(timeSpan), cancellationToken);
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void CodeFixProvider_MemberAccessExpression()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                Task.Delay(timeSpan, CancellationToken.None);
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
            await context.CreateTimer(context.CurrentUtcDateTime.Add(timeSpan), CancellationToken.None);
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void CodeFixProvider_TimeSpanOnly()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
                Task.Delay(timeSpan);
            }
        }
    }";

            var ExpectedFix = @"
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var timeSpan = new TimeSpan();
            await context.CreateTimer(context.CurrentUtcDateTime.Add(timeSpan), CancellationToken.None);
            }
        }
    }";

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
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
