// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class DependencyInjectionAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = DependencyInjectionAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void DI_NoDiagnosticTestCase()
        {
            var test = @"
    using System;
    using System.Threading;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public class HelloSequence
        {
            private string injectedVariable;

            public HelloSequence(string injectedVariable)
            {
                this.injectedVariable = injectedVariable;
            }

            [FunctionName(""DIAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var noninjectedVariable = ""test"";
            }

            [FunctionName(""DI_Activity"")]
            public static void DIActivity([ActivityTrigger] IDurableActivityContext context)
            {
                var usingDIVar = injectedVariable;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DI_InjectedSameNameTest()
        {
            var test = @"
    using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public class HelloSequence
    {
        private string injectedVariable;

        public HelloSequence(string injectedVariable)
        {
            this.injectedVariable = injectedVariable;
        }

        [FunctionName(""DIAnalyzerTestCases"")]
        public async Task Run(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var usingDIVar = injectedVariable;
                var usingDISimpleMemberAccessExpression = this.injectedVariable;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult[2];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "injectedVariable"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 22, 34)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "injectedVariable"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 64)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void DI_InjectedDifferentNameTest()
        {
            var test = @"
    using System;
    using System.Threading;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public class HelloSequence
        {
            private string diffVariableName;

            public HelloSequence(string injectedVariable)
            {
                this.diffVariableName = injectedVariable;
            }

            [FunctionName(""DIAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
               var usingDIVar = diffVariableName;
                var usingDISimpleMemberAccessExpression = this.diffVariableName;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult[2];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "diffVariableName"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 22, 33)
                        }
            };

            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "diffVariableName"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 64)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DeterministicMethodAnalyzer();
        }
    }
}
