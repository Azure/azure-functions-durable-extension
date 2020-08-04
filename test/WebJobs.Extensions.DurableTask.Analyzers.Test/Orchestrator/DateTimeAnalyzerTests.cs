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
    public class DateTimeAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = DateTimeAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string allNonDiagnosticTestCases = @"
        public async Task dateTimeNow()
        {
            System.DateTime.Now;
            System.DateTime.UtcNow;
            System.DateTime.Today;
			DateTime.Now;
			DateTime.UtcNow;
            DateTime.Today;
        }
    }
}";

        private const string ExpectedFix = @"
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var dateTime = context.CurrentUtcDateTime;
            }
        }
    }";

        [TestMethod]
        public void DateTime_NotOrchestrator_NoDiagnosticTestCases()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            " + allNonDiagnosticTestCases;

            VerifyCSharpDiagnostic(test);
        }
        
        [TestMethod]
        public void DateTime_Now_WithNamespace()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var dateTime = System.DateTime.Now;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.Now"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 32)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void DateTime_UtcNow_WithNamespace()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var dateTime = System.DateTime.UtcNow;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.UtcNow"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 32)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void DateTime_Today_WithNamespace()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var dateTime = System.DateTime.Today;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.Today"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 32)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void DateTime_Now()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var dateTime = DateTime.Now;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Now"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 32)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void DateTime_UtcNow()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var dateTime = DateTime.UtcNow;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.UtcNow"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 32)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void DateTime_Today()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var dateTime = DateTime.Today;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Today"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 32)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void DateTime_NonDeterministicMethod_AllDateTimeCases()
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
            [FunctionName(""DateTimeAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                DirectCall();
            }

            public static string DirectCall()
            {
                " + allNonDiagnosticTestCases;


            var expectedDiagnostics = new DiagnosticResult[7];
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
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.Now"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 13)
                        }
            };

            expectedDiagnostics[2] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.UtcNow"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 24, 13)
                        }
            };

            expectedDiagnostics[3] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.Today"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 25, 13)
                        }
            };

            expectedDiagnostics[4] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Now"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 26, 4)
                        }
            };

            expectedDiagnostics[5] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.UtcNow"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 27, 4)
                        }
            };

            expectedDiagnostics[6] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Today"),
                Severity = Severity,
                Locations =
                   new[] {
                            new DiagnosticResultLocation("Test0.cs", 28, 13)
                       }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new DateTimeCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OrchestratorAnalyzer();
        }
    }
}

