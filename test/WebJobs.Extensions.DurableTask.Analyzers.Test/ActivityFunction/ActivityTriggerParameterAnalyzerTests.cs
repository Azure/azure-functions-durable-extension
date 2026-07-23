// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.ActivityFunction
{
    [TestClass]
    public class ActivityTriggerParameterAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = ActivityTriggerParameterAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void ActivityTrigger_ParameterNotNamedData_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MyActivity"")]
            public static bool Run(
                [ActivityTrigger] (string, int) request)
            {
                return true;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ActivityTrigger_ParameterNamedData_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MyActivity"")]
            public static bool Run(
                [ActivityTrigger] (string, int) data)
            {
                return true;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityTriggerParameterAnalyzerMessageFormat, "data"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 49)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ActivityTrigger_ParameterNamedData_CaseInsensitive_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MyActivity"")]
            public static bool Run(
                [ActivityTrigger] string Data)
            {
                return true;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityTriggerParameterAnalyzerMessageFormat, "Data"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 42)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ActivityTrigger_WithActivityNameArgument_ParameterNamedData_Diagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName(""MyActivity"")]
            public static bool Run(
                [ActivityTrigger(Activity = ""MyActivity"")] string data)
            {
                return true;
            }
        }
    }";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityTriggerParameterAnalyzerMessageFormat, "data"),
                Severity = Severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 67)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void NonActivityTrigger_ParameterNamedData_NoDiagnostic()
        {
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            public static bool NotAnActivity(string data)
            {
                return true;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ActivityTrigger_ParameterNamedData_NotInsideFunction_NoDiagnostic()
        {
            // Regression guard: DF0115 must only fire on actual Azure Functions. A helper, sample, or
            // unit-test method that is annotated with [ActivityTrigger] but is NOT a Function (no [FunctionName])
            // must NOT be flagged. This is identical to ActivityTrigger_ParameterNamedData_Diagnostic except the
            // [FunctionName] attribute is absent, so the IsInsideFunction guard suppresses the diagnostic.
            var test = @"
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;

    namespace VSSample
    {
        public static class HelloSequence
        {
            public static bool Run(
                [ActivityTrigger] (string, int) data)
            {
                return true;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void IsolatedWorkerActivityTrigger_ParameterNamedData_NoDiagnostic()
        {
            // The isolated-worker ActivityTrigger attribute shares the simple name "ActivityTrigger" with the
            // in-proc (WebJobs) attribute, but it binds input via an InputConverter rather than the WebJobs
            // binding-data contract, so naming its parameter 'data' is harmless and must NOT be flagged.
            // Isolated-worker functions are marked with [Function], not [FunctionName]; because IsInsideFunction
            // looks specifically for [FunctionName] (the in-proc marker), the guard excludes this method. Both the
            // Function and ActivityTrigger attributes are stubbed inline so the test does not depend on the
            // isolated-worker package.
            var test = @"
    using System;

    namespace Microsoft.Azure.Functions.Worker
    {
        [AttributeUsage(AttributeTargets.Method)]
        public sealed class FunctionAttribute : Attribute
        {
            public FunctionAttribute(string name) { }
        }

        [AttributeUsage(AttributeTargets.Parameter)]
        public sealed class ActivityTriggerAttribute : Attribute
        {
            public string Activity { get; set; }
        }
    }

    namespace VSSample
    {
        using Microsoft.Azure.Functions.Worker;

        public static class HelloSequence
        {
            [Function(""MyActivity"")]
            public static bool Run(
                [ActivityTrigger] string data)
            {
                return true;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ActivityTriggerParameterAnalyzer();
        }
    }
}
