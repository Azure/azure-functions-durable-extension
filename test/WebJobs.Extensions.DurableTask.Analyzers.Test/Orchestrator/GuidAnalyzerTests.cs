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
        private readonly string diagnosticId = GuidAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = GuidAnalyzer.severity;

        private readonly string allTests = @"
        public void guidAllCalls()
        {
            Guid.NewGuid();
            System.Guid.NewGuid();
        }
    }
}";

        private readonly string fix = @"
    using System;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
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
            [FunctionName('E1_HelloSequence')]
            public static void Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                Guid.NewGuid();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Guid.NewGuid"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix, allowNewCompilerDiagnostics: true);
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
            [FunctionName('E1_HelloSequence')]
            public static void Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                System.Guid.NewGuid();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Guid.NewGuid"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            VerifyCSharpFix(test, fix, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void NewGuidInMethod_DeterministicAttribute()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        [Deterministic]
        " + allTests;

            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Guid.NewGuid"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 13)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Guid.NewGuid"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new GuidCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new GuidAnalyzer();
        }
    }
}
