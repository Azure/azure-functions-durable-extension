// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Binding
{
    [TestClass]
    public class ClientAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = ClientAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = ClientAnalyzer.severity;

        [TestMethod]
        public void DurableClient_V1_NonIssue()
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
            [OrchestrationClient] DurableOrchestrationClient client, ILogger log)
            {
            }
}";
            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DurableClient_V2_NonIssue()
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
            [DurableClient] IDurableClient durableClient, [DurableClient] IDurableOrchestrationClient durableOrchestrationClient, 
            [DurableClient] IDurableEntityClient durableEntityClient, ILogger log)
            {
            }
}";
            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void OrchestrationClient_V1_Object()
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
            [OrchestrationClient] Object context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void OrchestrationClient_V1_String()
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
            [OrchestrationClient] string context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void OrchestrationClient_V1_V2DurableInterface()
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
            [OrchestrationClient] IDurableClient context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "IDurableClient"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void OrchestrationClient_V1_Tuple()
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
            [OrchestrationClient] Tuple<int, string> context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V1ClientAnalyzerMessageFormat, "Tuple<int, string>"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 35)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void DurableClient_V2_Object()
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
            [DurableClient] Object context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "Object"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void DurableClient_V2_String()
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
            [DurableClient] string context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "string"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void DurableClient_V2_V1DurableClass()
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
            [DurableClient] DurableOrchestrationClient context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "DurableOrchestrationClient"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void DurableClient_V2_Tuple()
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
            [DurableClient] Tuple<int, string> context,
            ILogger log)
            {
            }
}";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = string.Format(Resources.V2ClientAnalyzerMessageFormat, "Tuple<int, string>"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 17, 29)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(test, expected);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ClientCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ClientAnalyzer();
        }
    }
}
