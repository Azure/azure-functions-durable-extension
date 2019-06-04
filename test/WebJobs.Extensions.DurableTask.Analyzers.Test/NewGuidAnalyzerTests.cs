using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using TestHelper;
using WebJobs.Extensions.DurableTask.Analyzers;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Analyzers.Test
{
    public class NewGuidMethodCallTest : DiagnosticVerifier
    {
        [Fact]
        public void NoUsageDoesNotTriggerWarning()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NewGuidCall_InOrderchestrationContext_ReturnsWarning()
        {
            var test = @"
    using System;
    using Microsoft.Azure.WebJobs;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            [FunctionName(""HistoricalFunction"")]
            public static async Task RunOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context)
            {
                Guid.NewGuid();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "NewGuidAnalyzer",
                Message = "Guid.NewGuid() method calls are not allowed in DurableOrchestrationContext",
                Severity = DiagnosticSeverity.Warning,
                Locations = new[]
              {
          new DiagnosticResultLocation("Test0.cs", 13, 22)
        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new NewGuidAnalyzer();
        }
    }
}
