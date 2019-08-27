using DurableFunctionsAnalyzer.analyzers;
using DurableFunctionsAnalyzer.codefixproviders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;

namespace DurableFunctionsAnalyzer.Test

{
    [TestClass]
    public class DateTimeAnalyzerTests : CodeFixVerifier
    {

        String fixtest = @"
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
                context.UtcNow;
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
        public void DateTimeInOrchestrator_System_Now()
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
                Id = "DateTimeAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "System.DateTime.Now"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
            
            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DateTimeInOrchestrator_System_UtcNow()
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
                Id = "DateTimeAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "System.DateTime.UtcNow"),
                Severity = DiagnosticSeverity.Error,
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
                Id = "DateTimeAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "DateTime.Now"),
                Severity = DiagnosticSeverity.Error,
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
                Id = "DateTimeAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "DateTime.UtcNow"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DateTimeInMethod_IsDeterministicAttribute_Now()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        [IsDeterministic]
        public int testDeterministicMethod()
        {
            DateTime.Now;
            return 5;
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "DateTimeAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "DateTime.Now"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 10, 13)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
            
            // Test fix
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

