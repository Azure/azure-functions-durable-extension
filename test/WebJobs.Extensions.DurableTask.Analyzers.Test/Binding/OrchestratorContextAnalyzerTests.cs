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
    public class OrchestratorContextAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = OrchestratorContextAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private const string V1ExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] DurableOrchestrationContext context)
            {
            }
    }
}";

        private const string V1BaseExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] DurableOrchestrationContextBase context)
            {
            }
    }
}";

        private const string V2ExpectedFix = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
            }
    }
}";

        [TestMethod]
        public void OrchestrationContext_V1_NoDiagnosticTestCases()
        {
            SyntaxNodeUtils.version = DurableVersion.V1;
            VerifyCSharpDiagnostic(V1ExpectedFix);
            VerifyCSharpDiagnostic(V1BaseExpectedFix);
        }

        [TestMethod]
        public void OrchestrationContext_V2_NoDiagnosticTestCases()
        {
            SyntaxNodeUtils.version = DurableVersion.V2;
            VerifyCSharpDiagnostic(V2ExpectedFix);
        }

        // Tests SyntaxKind.IdentifierName
        [TestMethod]
        public void OrchestrationContext_V1_UsingObject()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] Object context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        // Tests SyntaxKind.PredefinedType
        [TestMethod]
        public void OrchestrationContext_V1_UsingString()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] string context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        // Tests SyntaxKind.GenericName
        [TestMethod]
        public void OrchestrationContext_V1_UsingList()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] List<string> context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "List<string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        // Tests SyntaxKind.ArrayType
        [TestMethod]
        public void OrchestrationContext_V1_UsingArray()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] string[] context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "string[]"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        // Tests SyntaxKind.TupleType
        [TestMethod]
        public void OrchestrationContext_V1_UsingValueTuple()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] (string, int) context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "(string, int)"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        // Tests Durable V2 Context
        [TestMethod]
        public void OrchestrationContext_V1_UsingV2Context()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V1OrchestratorContextAnalyzerMessageFormat, "IDurableOrchestrationContext"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 12, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V1;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V1ExpectedFix, 0);
            VerifyCSharpFix(test, V1BaseExpectedFix, 1);
        }

        // Tests SyntaxKind.IdentifierName
        [TestMethod]
        public void OrchestrationContext_V2_UsingObject()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] Object context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "Object"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        // Tests SyntaxKind.PredefinedType
        [TestMethod]
        public void OrchestrationContext_V2_UsingString()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] string context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        // Tests SyntaxKind.GenericName
        [TestMethod]
        public void OrchestrationContext_V2_UsingList()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] List<string> context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "List<string>"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix, allowNewCompilerDiagnostics: true);
        }

        // Tests SyntaxKind.ArrayType
        [TestMethod]
        public void OrchestrationContext_V2_UsingArray()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] string[] context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "string[]"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        // Tests SyntaxKind.TupleType
        [TestMethod]
        public void OrchestrationContext_V2_UsingValueTuple()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] (string, int) context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "(string, int)"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        // Tests Durable V1 Context
        [TestMethod]
        public void OrchestrationContext_V2_UsingV1Context()
        {
            var test = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""OrchestratorContextAnalyzerTestCases"")]
        public static async Task Run(
            [OrchestrationTrigger] DurableOrchestrationContext context)
            {
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.V2OrchestratorContextAnalyzerMessageFormat, "DurableOrchestrationContext"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 36)
                     }
            };

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, expectedDiagnostics);

            VerifyCSharpFix(test, V2ExpectedFix);
        }

        // Tests SyntaxNodeUtils.TryGetAttribute
        [TestMethod]
        public void OrchestrationContext_V2_InvalidOperationTest()
        {
            var test = @"
namespace VSSample
{
    public interface ExampleInterface
    {
        [return: System.ServiceModel.MessageParameterAttribute(Name = ""getSubscriberInfoReturn"")]
        string getSubscriberInfo(string request);
    }
}
";

            SyntaxNodeUtils.version = DurableVersion.V2;

            VerifyCSharpDiagnostic(test, new DiagnosticResult[] { });
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
