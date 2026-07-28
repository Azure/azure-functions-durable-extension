// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class ConfigureAwaitAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = ConfigureAwaitAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void ConfigureAwait_NoDiagnosticTestCase()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"");
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ConfigureAwait_False_OnActivityCall()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(false);
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "ConfigureAwait"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 23)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ConfigureAwait_ResultStoredThenAwaited_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var configuredAwaitable = context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(false);
                await configuredAwaitable;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "ConfigureAwait"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 43)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ConfigureAwait_ResultAssignedThenAwaited_Diagnostic()
        {
            var test = @"
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                ConfiguredTaskAwaitable<string> configuredAwaitable;
                configuredAwaitable = context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(false);
                await configuredAwaitable;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "ConfigureAwait"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 39)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ConfigureAwait_ResultAssignedInsideAwait_Diagnostic()
        {
            var test = @"
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                ConfiguredTaskAwaitable<string> configuredAwaitable;
                await (configuredAwaitable = context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(false));
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "ConfigureAwait"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 16, 46)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ConfigureAwait_True_OnActivityCall_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(true);
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ConfigureAwait_ConstantTrue_OnActivityCall_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                const bool continueOnCapturedContext = true;
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(continueOnCapturedContext);
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ConfigureAwait_MultipleArgumentsIncludingTrue_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public class ConfigurableOperation
        {
            public Task ConfigureAwait(bool first, bool second)
            {
                return Task.CompletedTask;
            }
        }

        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await new ConfigurableOperation().ConfigureAwait(false, true);
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"");
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "ConfigureAwait"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 22, 23)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ConfigureAwait_NotAwaited_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public class ConfigurableOperation
        {
            public void ConfigureAwait(bool continueOnCapturedContext)
            {
            }
        }

        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                new ConfigurableOperation().ConfigureAwait(false);
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"");
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ConfigureAwait_ResultStoredButNotAwaited_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public class ConfigurableOperation
        {
            public Task ConfigureAwait(bool continueOnCapturedContext)
            {
                return Task.CompletedTask;
            }
        }

        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var configuredAwaitable = new ConfigurableOperation().ConfigureAwait(false);
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"");
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ConfigureAwait_NonLiteralArgument_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var continueOnCapturedContext = false;
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(continueOnCapturedContext);
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "ConfigureAwait"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 23)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ConfigureAwait_InMethodCalledByOrchestrator()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await DirectCall(context);
            }

            public static async Task DirectCall(IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"").ConfigureAwait(false);
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult[2];
            expectedDiagnostics[0] = new DiagnosticResult
            {
                Id = MethodInvocationAnalyzer.DiagnosticId,
                Message = string.Format(Resources.MethodAnalyzerMessageFormat, "DirectCall(context)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 23)
                        }
            };
            expectedDiagnostics[1] = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.DeterministicAnalyzerMessageFormat, "ConfigureAwait"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 19, 23)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ConfigureAwait_OutsideOrchestrator_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
            }

            public static async Task NotAnOrchestrator()
            {
                await Task.Delay(1).ConfigureAwait(false);
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ConfigureAwait_AsMemberAccessExpression_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class ConfigureAwait
        {
            public static void Foo(bool value)
            {
            }
        }

        public static class HelloSequence
        {
            [FunctionName(""ConfigureAwaitAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                ConfigureAwait.Foo(false);
                await context.CallActivityAsync<string>(""Function1_Hello"", ""Tokyo"");
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DeterministicMethodAnalyzer();
        }
    }
}
