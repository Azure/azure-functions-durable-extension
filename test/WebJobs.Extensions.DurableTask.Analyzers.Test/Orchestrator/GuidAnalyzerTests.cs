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
    public class GuidAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = GuidAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string allTests = @"
        public void guidAllCalls()
        {
            Guid.NewGuid();
            System.Guid.NewGuid();
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
            [FunctionName(""GuidAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.NewGuid();
            }
        }
    }";

        [TestMethod]
        public void Guid_NoDiagnosticTestCases()
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
            [FunctionName(""GuidAnalyzerTestCases"")]
            " + allTests;

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Guid_NewGuid()
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
            [FunctionName(""GuidAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Guid.NewGuid();
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Guid.NewGuid"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void Guid_NewGuid_WithNamespace()
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
            [FunctionName(""GuidAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                System.Guid.NewGuid();
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Guid.NewGuid"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void Guid_NonDeterministicMethod_AllGuidCases()
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
            [FunctionName(""GuidAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                DirectCall();
            }

        public static string DirectCall()
        {
            " + allTests;


            var expectedDiagnostics = new DiagnosticResult[3];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = MethodInvocationAnalyzer.DiagnosticId,
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
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Guid.NewGuid"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 13)
                        }
            };

            expectedDiagnostics[2] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Guid.NewGuid"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 24, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new GuidCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DeterministicMethodAnalyzer();
        }
    }
}
