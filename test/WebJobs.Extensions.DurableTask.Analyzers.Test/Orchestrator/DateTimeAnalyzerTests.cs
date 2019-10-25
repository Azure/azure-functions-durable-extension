// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator

{
    [TestClass]
    public class DateTimeAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = DateTimeAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = DateTimeAnalyzer.severity;
        private readonly string fixtest = @"
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
context.CurrentUtcDateTime;
            }
        }
    }";

        [TestMethod]
        public void DateTimeInMethod_NonIssueCalls()
        {
            var test = @"
    using System;

    namespace VSSample
    {
        public static class DateTimeNowExample
        {
            public void dateTimeNow()
            {
			    System.DateTime.Now;
			    System.DateTime.UtcNow;
			    DateTime.Now;
			    DateTime.UtcNow;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }
        
        [TestMethod]
        public void DateTimeInOrchestrator_Now_Namespace()
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
                System.DateTime.Now;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.Now"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
            
            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DateTimeInOrchestrator_UtcNow_Namespace()
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
                System.DateTime.UtcNow;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "System.DateTime.UtcNow"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DateTimeInOrchestrator_Now()
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
                DateTime.Now;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Now"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DateTimeInOrchestrator_UtcNow()
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
                DateTime.UtcNow;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.UtcNow"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DateTimeInMethod_DeterministicAttribute_Now()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        [Deterministic]
        public int testDeterministicMethod()
        {
            DateTime.Now;
            return 5;
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "DateTime.Now"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 10, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtestAttribute = @"
    using System;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        
        public int testDeterministicMethod()
        {
            DateTime.Now;
            return 5;
        }
    }";

            //VerifyCSharpFix(test, fixtestAttribute);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new DateTimeCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DateTimeAnalyzer();
        }
    }
}

