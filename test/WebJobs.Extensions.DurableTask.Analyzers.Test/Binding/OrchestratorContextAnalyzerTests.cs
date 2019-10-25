// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Binding
{
    [TestClass]
    public class OrchestratorContextAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = OrchestratorContextAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = OrchestratorContextAnalyzer.severity;
        private readonly string fixtestV2 = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";

        [TestMethod]
        public void V2_DurableInterface_NonIssue()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";
            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void V2_NonDurableInterface_Object()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] Object context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void V2_NonDurableInterface_String()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] string context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void V2_V1DurableClass()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "DurableOrchestrationContext"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void V1_DurableContext_NonIssue()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";
            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void V1_DurableContextBase_NonIssue()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContextBase context,
            ILogger log)
            {
               
            }
}";
            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void V1_NonDurable_Object()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] Object context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void V1_NonDurable_String()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] string context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void V1_V2DurableInterface()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExternalInteraction
{
    public static class HireEmployee
    {
        [FunctionName(""HireEmployee"")]
        public static async Task<Application> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
            {
               
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "IDurableOrchestrationContext"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test, expected);

            //VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new OrchestratorContextCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new OrchestratorContextAnalyzer();
        }
    }
}
