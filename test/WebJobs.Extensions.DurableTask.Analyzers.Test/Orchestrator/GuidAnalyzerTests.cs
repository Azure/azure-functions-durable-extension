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
        private static readonly DiagnosticSeverity Severity = GuidAnalyzer.Severity;

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
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static void Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.NewGuid();
            }
        }
    }";

        [TestMethod]
        public void NewGuid_NonIssueCalls()
        {
            var test = @"
    using System;

    namespace VSSample
    {
        public static class GuidNewGuidExample
        {
            [FunctionName(""E1_HelloSequence"")]
            " + allTests;

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NewGuidInOrchestrator()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static void Run(
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
                            new DiagnosticResultLocation("Test0.cs", 14, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void NewGuidInOrchestrator_Namespace()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""E1_HelloSequence"")]
            public static void Run(
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
                            new DiagnosticResultLocation("Test0.cs", 14, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void NewGuid_InMethod_OrchestratorCall_All()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;
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


            var expectedDiagnostics = new DiagnosticResult[3];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = MethodAnalyzer.DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "DirectCall()"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 17)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Guid.NewGuid"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 24, 13)
                        }
            };

            expectedDiagnostics[2] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Guid.NewGuid"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 25, 13)
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
            return new OrchestratorAnalyzer();
        }
    }
}
