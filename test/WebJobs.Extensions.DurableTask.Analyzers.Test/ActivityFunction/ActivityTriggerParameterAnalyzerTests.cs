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

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ActivityTriggerParameterAnalyzer();
        }
    }
}
