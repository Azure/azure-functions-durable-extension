// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.EntityInterface
{
    [TestClass]
    public class InterfaceContentAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = InterfaceContentAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = InterfaceContentAnalyzer.severity;

        [TestMethod]
        public void InterfaceContentAnalyzer_NonIssue()
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
                context.SignalEntityAsync<IEntityExample>();
            }
        }

        public interface IEntityExample
        {
            public static void methodTest();
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void InterfaceContentAnalyzer_NoMethods()
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
                context.SignalEntityAsync<IEntityExample>();
            }
        }

        public interface IEntityExample
        {
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = Resources.EntityInterfaceContentAnalyzerNoMethodsMessageFormat,
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 19, 9)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void InterfaceContentAnalyzer_Property()
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
                context.SignalEntityAsync<IEntityExample>();
            }
        }

        public interface IEntityExample
        {
            public string PropertyTest {get; set};

            public static void methodTest();
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityInterfaceContentAnalyzerMessageFormat, "public string PropertyTest {get; set};"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new InterfaceAnalyzer();
        }
    }
}
