// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class EnvironmentVariableAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = EnvironmentVariableAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = EnvironmentVariableAnalyzer.Severity;

        private readonly string allTests = @"
        public void environmentAllCalls()
        {
            Environment.GetEnvironmentVariable(""test"");
            Environment.GetEnvironmentVariables();
            Environment.ExpandEnvironmentVariables(""test"");
            System.Environment.GetEnvironmentVariable(""test"");
            System.Environment.GetEnvironmentVariables();
            System.Environment.ExpandEnvironmentVariables(""test"");
        }
    }
}";

        [TestMethod]
        public void EnvironmentVariables_NonIssueCalls()
        {
            var test = @"
    using System;

    namespace VSSample
    {
        public static class EnvironmentExample
        {
            " + allTests;

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void GetEnvironmentVariable_InOrchestrator()
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
                Environment.GetEnvironmentVariable(""test"");
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Environment.GetEnvironmentVariable"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void GetEnvironmentVariable_InOrchestrator_Namespace()
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
                System.Environment.GetEnvironmentVariable(""test"");
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Environment.GetEnvironmentVariable"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void GetEnvironmentVariables_InOrchestrator()
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
                Environment.GetEnvironmentVariables();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Environment.GetEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void GetEnvironmentVariables_InOrchestrator_Namespace()
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
                System.Environment.GetEnvironmentVariables();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Environment.GetEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_InOrchestrator()
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
                Environment.ExpandEnvironmentVariables(""test"");
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Environment.ExpandEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ExpandEnvironmentVariables_InOrchestrator_Namespace()
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
                System.Environment.ExpandEnvironmentVariables(""test"");
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Environment.ExpandEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void GetEnvironmentVariables_InMethod_DeterministicAttribute()
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


            var expectedResults = new DiagnosticResult[7];
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
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Environment.GetEnvironmentVariable"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 27, 13)
                        }
            };

            expectedResults[2] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Environment.GetEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 28, 13)
                        }
            };

            expectedResults[3] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "Environment.ExpandEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 29, 13)
                        }
            };

            expectedResults[4] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Environment.GetEnvironmentVariable"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 30, 13)
                        }
            };

            expectedResults[5] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Environment.GetEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 31, 13)
                        }
            };

            expectedResults[6] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "System.Environment.ExpandEnvironmentVariables"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 32, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OrchestratorAnalyzer();
        }
    }
}
