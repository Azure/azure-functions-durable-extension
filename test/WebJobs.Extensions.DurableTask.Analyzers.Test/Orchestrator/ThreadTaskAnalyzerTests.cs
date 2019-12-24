// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class ThreadTaskAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = ThreadTaskAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = ThreadTaskAnalyzer.severity;

        private readonly string allTests = @"
            public void threadTaskAllCalls()
            {
                await System.Threading.Tasks.Task.Run(() => { });
                await System.Threading.Tasks.Task.Factory.StartNew(() => { });
                await Task.Run(() => { });
                await Task.Factory.StartNew(() => { });

                var t = new Thread();
                t.Start();
                var t2 = new System.Threading.Thread();
                t2.Start();

                Task task = new Task();
                task.ContinueWith((t, o) => { }, new object());
                System.Threading.Tasks.Task task2 = new System.Threading.Tasks.Task();
                task2.ContinueWith((t, o) => { }, new object());

                task.ContinueWith((t, o) => { }, new object(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
}
    }";

        [TestMethod]
        public void ThreadTaskInMethod_NonIssueCalls()
        {
            var test = @"
    using System;

    namespace VSSample
    {
        public static class ThreadTaskNonIssueCalls
        {
            " + allTests;

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TaskInOrchestrator_Run_Namespace()
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
                System.Threading.Tasks.Task.Run(() => { });
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Run"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TaskInOrchestrator_Run()
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
                Task.Run(() => { });
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Run"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TaskInOrchestrator_Factory_StartNew_Namespace()
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
                System.Threading.Tasks.Task.Factory.StartNew(() => { });
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Factory.StartNew"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TaskInOrchestrator_Factory_StartNew()
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
                Task.Factory.StartNew(() => { });
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Factory.StartNew"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ThreadInOrchestrator_Start_Namespace()
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
                var t = new System.Threading.Thread(() => { });
                t.Start();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "t.Start"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ThreadInOrchestrator_Start()
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
                var t = new Thread(() => { });
                t.Start();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "t.Start"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TaskInOrchestrator_ContinueWith_Namespace()
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
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                System.Threading.Tasks.Task task = new System.Threading.Tasks.Task();
                task.ContinueWith((t, o) => { }, new object());
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "task.ContinueWith"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TaskInOrchestrator_ContinueWith()
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
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Task task = new Task();
                task.ContinueWith((t, o) => { }, new object());
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "task.ContinueWith"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void TaskInOrchestrator_ContinueWith_ExecuteSynchronously()
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
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Task task = new Task();
                task.ContinueWith((t, o) => { }, new object(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ThreadTask_DeterministicAttribute_All()
        {
            var test = @"
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        [Deterministic]
        " + allTests;

            var expectedResults = new DiagnosticResult[8];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Run"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 23)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Threading.Tasks.Task.Factory.StartNew"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 23)
                        }
            };

            expectedResults[2] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Run"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 23)
                        }
            };

            expectedResults[3] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Task.Factory.StartNew"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 23)
                        }
            };

            expectedResults[4] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "t.Start"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 19, 17)
                        }
            };

            expectedResults[5] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "t2.Start"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 17)
                        }
            };

            expectedResults[6] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "task.ContinueWith"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 24, 17)
                        }
            };

            expectedResults[7] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "task2.ContinueWith"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 26, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ThreadTaskAnalyzer();
        }
    }
}
