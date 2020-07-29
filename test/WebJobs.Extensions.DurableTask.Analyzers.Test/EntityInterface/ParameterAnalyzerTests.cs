// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.EntityInterface
{
    [TestClass]
    public class ParameterAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = ParameterAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void ParameterAnalyzer_NoDiagnosticTestCases()
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
            [FunctionName(""ParameterAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<IEntityExample>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();

            public static void methodTestOneParameter(string test);
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        // Entity interface must have no more than 1 parameter.
        [TestMethod]
        public void ParameterAnalyzer_MoreThanOne()
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
            [FunctionName(""ParameterAnalyzerTestCases"")]
            public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                context.SignalEntityAsync<IEntityExample>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTestTwoParameters(string test, int number);
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityInterfaceParameterAnalyzerMessageFormat, "public static void methodTestTwoParameters(string test, int number);"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 13)
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
