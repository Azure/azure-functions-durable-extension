// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.ActivityFunction
{
    [TestClass]
    public class FunctionReturnTypeAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = FunctionReturnTypeAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = FunctionReturnTypeAnalyzer.Severity;

        [TestMethod]
        public void ReturnType_NonIssueCalls()
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
        // Should not flag code on non function
        public static async Task<List<string>> NonFunctionWrongReturnType(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                await context.CallActivityAsync<int>(""E1_SayHello"", ""Tokyo"");
                await context.CallActivityAsync<int>(""E1_SayHey"", ""Tokyo"");
                await context.CallActivityAsync<int>(""E1_SayHello_Tuple"", (""Seattle"", 4));
            
                return outputs;
            }

        [FunctionName(""E1_HelloSequence"")]
        public static async Task<List<string>> CorrectReturnType(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                // Testing different matching return types
                await context.CallActivityAsync<string>(""E1_SayHello"", ""Tokyo"");
                await context.CallActivityAsync<string[]>(""E1_SayHello_Array"", ""Tokyo"");
                await context.CallActivityAsync<Object>(""E1_SayHey"", ""Tokyo"");
                await context.CallActivityAsync<Tuple<string, int>>(""E1_SayHello_Tuple"", (""Seattle"", 4));

                // Should always be valid without specifying return type
                await context.CallActivityAsync(""E1_SayHello_ReturnsString"", ""London"");

                // ArrayType and NamedType (IEnumerable types) match
                await context.CallActivityAsync<string[]>(""E1_SayHello_ArrayAndNamedType"", (""Seattle"", 4));
                
                // NamedType and NamedType (IEnumerable types) match
                await context.CallActivityAsync<List<string>>(""E1_SayHello_NamedType"", ""London"");

                // string and Task<string>
                await context.CallActivityAsync<string>(""E1_SayHello_Task"", ""London"");
            
                return outputs;
            }

        [FunctionName(""E1_SayHello"")]
        public static string SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_Array"")]
        public static string[] SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return new [] { $@""Hello {name}!"" };
        }

        [FunctionName(""E1_SayHey"")]
        public static Object SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return new Object();
        }

        [FunctionName(""E1_SayHello_Tuple"")]
        public static Tuple<string, int> SayHelloTuple([ActivityTrigger] Tuple<string, int> tuple)
        {
            return tuple;
        }

        [FunctionName(""E1_SayHello_ReturnsString"")]
        public static string SayHelloDirectInput([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_ArrayAndNamedType"")]
        public static List<string> SayHelloTuple([ActivityTrigger] Tuple<string, int> tuple)
        {
            return new List<string>();
        }

        [FunctionName(""E1_SayHello_NamedType"")]
        public static IList<string> SayHelloDirectInput([ActivityTrigger] string name)
        {
            return new List<string>();
        }

        [FunctionName(""E1_SayHello_Task"")]
        public static Task<string> SayHelloDirectInput([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ReturnType_Mismatch_StringAndInt()
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
        [FunctionName(""E1_HelloSequence"")]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                await context.CallActivityAsync<int>(""E1_SayHello"", ""test"");
            
                return outputs;
            }

        [FunctionName(""E1_SayHello"")]
        public static string SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityReturnTypeAnalyzerMessageFormat, "E1_SayHello", "string", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 23)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ReturnType_Mismatch_IEnumerableTypes_StringAndList()
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
        [FunctionName(""E1_HelloSequence"")]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                await context.CallActivityAsync<string>(""E1_SayHello"", ""test"");
            
                return outputs;
            }

        [FunctionName(""E1_SayHello"")]
        public static List<string> SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityReturnTypeAnalyzerMessageFormat, "E1_SayHello", "System.Collections.Generic.List<string>", "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 23)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ReturnType_Mismatch_StringAndTask()
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
        [FunctionName(""E1_HelloSequence"")]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""World""));
            
                return outputs;
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
                Message = string.Format(Resources.ActivityReturnTypeAnalyzerMessageFormat, "E1_SayHello", "System.Threading.Tasks.Task", "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 35)
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
