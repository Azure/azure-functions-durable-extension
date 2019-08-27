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
    public class GuidAnalyzerTests : CodeFixVerifier
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
                context.NewGuid();
            }
        }
    }";

        [TestMethod]
        public void NewGuid_NonIssueCalls()
        {
            var test = @"
    using System;

    namespace VSSample
    {
        public static class GuidNewGuidExample
        {
            public void dateTimeNow()
            {
			    Guid.NewGuid();
                System.Guid.NewGuid();
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NewGuidInOrchestrator()
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
                Guid.NewGuid();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "GuidAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "Guid.NewGuid"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 22)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void NewGuidInOrchestrator_System()
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
                System.Guid.NewGuid();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "GuidAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "System.Guid.NewGuid"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 29)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void NewGuidInMethod_IsDeterministicAttribute()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        [IsDeterministic]
        public int testDeterministicMethod()
        {
            Guid.NewGuid();
            return 5;   
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "GuidAnalyzer",
                Message = String.Format("'{0}' violates the orchestrator deterministic code constraint", "Guid.NewGuid"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 10, 18)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            // No fix currently in place
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new GuidCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new GuidAnalyzer();
        }
    }
}
