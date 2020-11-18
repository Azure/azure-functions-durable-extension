// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.ActivityFunction
{
    [TestClass]
    public class NameAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = NameAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void Name_NoDiagnosticTestCases()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace VSSample
{
    public static class HelloSequence
    {
        // For test cases below
        public const string TestFunctionUsesConstant = ""TestFunctionNameWithConstant"";
        public const string TestFunctionNameWithClass = ""TestFunctionNameWithClass"";
        public const string TestFunctionInDependencies = ""TestFunctionInDependencies"";

        // Testing that no diagnostics are produced when method does not have the FunctionName attribute present
        public static async Task<string> NonFunctionInvalidNames(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            // Non existing function names
            await context.CallActivityAsync<string>(""NotAFunction"", ""Tokyo"");
            await context.CallActivityAsync<string>(""DefinitelyNotAFunction"", new Object());
            
            return ""Hello World"";
        }

        [FunctionName(""NameAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            // Matching names (strings)

            await context.CallActivityAsync<string>(""Test_MatchingStrings"", ""Prairie View"");

            // Invocation and function using nameof()

            await context.CallActivityAsync<string>(nameof(TestFunctionUsesNameOfClassName), ""Minneapolis"");

            // Invocation uses string, function uses nameof()

            await context.CallActivityAsync<string>(""TestFunctionUsesNameOfMethodName"", ""Brunswick"");

            // Invocation uses string, function uses constant

            await context.CallActivityAsync<string>(""TestFunctionNameWithConstant"", ""Minneapolis"");

            // Invocation uses string, function uses constant prefaced with class name

            await context.CallActivityAsync<string>(""TestFunctionNameWithClass"", ""Ann Arbor"");

            // Invocation uses constant, function uses constant

            await context.CallActivityAsync<string>(TestFunctionUsesConstant, ""Fort Worth"");

            // Invocation uses constant prefaced with class name, function uses nameof()

            await context.CallActivityAsync<string>(HelloSequence.TestFunctionNameWithClass, ""Louisville"");

            // Invocation uses constant, function not found in source code, covers <FunctionsInDependencies>true</FunctionsInDependencies>

            await context.CallActivityAsync<string>(TestFunctionInDependencies, ""Minneapolis"");

            return ""Hello World!"";
        }

        // Matching names (strings)

        [FunctionName(""Test_MatchingStrings"")]
        public static string TestMatchingStrings([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
        
        // Invocation uses string and nameof(), function uses nameof()

        [FunctionName(nameof(TestFunctionUsesNameOfMethodName))]
        public static string TestFunctionUsesNameOfMethodName([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        // Invocation uses string, function uses constant

        [FunctionName(TestFunctionUsesConstant)]
        public static string TestFunctionWithConstant([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        // Invocation uses string, function uses constant prefaced with class name

        [FunctionName(HelloSequence.TestFunctionNameWithClass)]
        public static string TestFunctionNameClass([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }

    // Invocation uses nameof() and constant with class name prefix and function using nameof()

    public class TestFunctionUsesNameOfClassName
    {
        [FunctionName(nameof(TestFunctionUsesNameOfClassName))]
        public string Run([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }
        
        [TestMethod]
        public void Name_InvalidName_CloseRule()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""NameAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""E1_SayHey"", 4);
            
                return ""Hello World!"";
            }

        [FunctionName(""E1_SayHello"")]
        public static Task SayHello([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityNameAnalyzerCloseMessageFormat, "E1_SayHey", "E1_SayHello"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 57)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Name_InvalidName_MissingRule()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""NameAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""E1_SayHello"", ""AppService"");
            
                return ""Hello World!"";
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityNameAnalyzerMissingMessageFormat, "E1_SayHello"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 57)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Name_InvalidName_MissingRule_UsingConstant()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace VSSample
{
    public static class HelloSequence
    {
        public const string FunctionName = ""E1_HelloSequence"";
        [FunctionName(FunctionName)]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""AppService""));
            
                return outputs;
            }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityNameAnalyzerMissingMessageFormat, "E1_SayHello"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 24, 69)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new FunctionAnalyzer();
        }
    }
}
