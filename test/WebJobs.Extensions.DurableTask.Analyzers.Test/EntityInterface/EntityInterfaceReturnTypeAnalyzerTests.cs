// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.EntityInterface
{
    [TestClass]
    public class EntityInterfaceReturnTypeAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = EntityInterfaceReturnTypeAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = EntityInterfaceReturnTypeAnalyzer.severity;

        [TestMethod]
        public void ReturnTypeAnalyzer_NonIssue()
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
            public static void methodTestVoid();

            public static Task methodTestTask(string test);

            public static Task<T> methodTestTaskT(string test);
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ReturnTypeAnalyzer_IncorrectReturn_Object()
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
            public static Object methodTestOneParameter(string test);
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityInterfaceReturnTypeAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ReturnTypeAnalyzer_IncorrectReturn_string()
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
            public static string methodTestOneParameter(string test);
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityInterfaceReturnTypeAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ReturnTypeAnalyzer_IncorrectReturn_Tuple()
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
            public static Tuple<int, string> methodTestOneParameter(string test);
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.EntityInterfaceReturnTypeAnalyzerMessageFormat, "Tuple<int, string>"),
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
