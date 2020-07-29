// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.EntityInterface
{
    [TestClass]
    public class InterfaceAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = InterfaceAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void InterfaceAnalyzer_NoDiagnosticTestCases()
        {
            var test = @"
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""InterfaceAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<IEntityExample>();
            }

            public static async Task NotFunction(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<IEntityExample>();
            }

            public static async Task NotFunctionNotEntity(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<IAmNotAnEntity>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        // Tests SyntaxKind.IdentifierName
        [TestMethod]
        public void InterfaceAnalyzer_SignalEntityUsingObject()
        {
            var test = @"
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""InterfaceAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<Object>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.SignalEntityAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 43)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        // Tests SyntaxKind.PredefinedType
        [TestMethod]
        public void InterfaceAnalyzer_SignalEntityUsingString()
        {
            var test = @"
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""InterfaceAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<string>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.SignalEntityAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 43)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        // Tests SyntaxKind.GenericName
        [TestMethod]
        public void InterfaceAnalyzer_SignalEntityUsingList()
        {
            var test = @"
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""InterfaceAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<List<string>>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.SignalEntityAnalyzerMessageFormat, "List<string>"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 43)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        // Tests SyntaxKind.ArrayType
        [TestMethod]
        public void InterfaceAnalyzer_SignalEntityUsingArray()
        {
            var test = @"
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""InterfaceAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<string[]>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.SignalEntityAnalyzerMessageFormat, "string[]"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 43)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        // Tests SyntaxKind.TupleType
        [TestMethod]
        public void InterfaceAnalyzer_SignalEntityUsingValueTuple()
        {
            var test = @"
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""InterfaceAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<(string, int)>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.SignalEntityAnalyzerMessageFormat, "(string, int)"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 43)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        // Tests interface not defined in user code
        [TestMethod]
        public void InterfaceAnalyzer_SignalEntityUsingILogger()
        {
            var test = @"
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""InterfaceAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<ILogger>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.SignalEntityAnalyzerMessageFormat, "ILogger"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 43)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new InterfaceAnalyzer();
        }
    }
}
