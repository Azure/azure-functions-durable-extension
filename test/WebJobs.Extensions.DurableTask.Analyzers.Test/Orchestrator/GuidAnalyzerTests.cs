﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        private readonly DiagnosticSeverity severity = GuidAnalyzer.Severity;

        private readonly string allTests = @"
        public void guidAllCalls()
        {
            Guid.NewGuid();
            System.Guid.NewGuid();
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
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void NewGuidInOrchestrator_Namespace()
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
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void NewGuidInMethod_DeterministicAttribute()
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


            var expectedResults = new DiagnosticResult[3];
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
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Guid.NewGuid"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 27, 13)
                        }
            };

            expectedResults[2] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Guid.NewGuid"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 28, 13)
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
            return new OrchestratorAnalyzer();
        }
    }
}
