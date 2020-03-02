// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.ActivityFunction
{
    [TestClass]
    public class ArgumentAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = ArgumentAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = ArgumentAnalyzer.Severity;

        [TestMethod]
        public void Argument_NonIssueCalls()
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
        public static async Task<List<string>> NotInsideFunctionWrongInput(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", 100));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_DirectInput"", 100));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Object"", 100));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Object_DirectInput"", 100));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple"", 100));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple_OnContext"", 100));
            
                return outputs;
            }

        [FunctionName(""E1_HelloSequence"")]
        public static async Task<List<string>> CorrectInput(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                // Testing different matching input types
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""Tokyo""));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_DirectInput"", ""London""));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Object"", new Object()));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Object_DirectInput"", new Object()));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple"", (""Seattle"", 4)));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple_OnContext"", (""Seattle"", 4)));

                // ArrayType and NamedType (IEnumerable types) match
                string[] arrayType = new string[] { ""Seattle"", ""Tokyo"" };
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_ArrayToNamed"", arrayType));
                
                // NamedType and NamedType (IEnumerable types) match
                List<string> namedType = new List<string>();
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_NamedToNamed"", namedType));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_NamedToNamed_Direct"", namedType));

                // null input when function input not used
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_NotUsed"", null));
            
                return outputs;
            }

        [FunctionName(""E1_SayHello"")]
        public static string SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_DirectInput"")]
        public static string SayHelloDirectInput([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_Object"")]
        public static string SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<Object>();
            return $""Hello World!"";
        }

        [FunctionName(""E1_SayHello_Object_DirectInput"")]
        public static string SayHelloDirectInput([ActivityTrigger] Object name)
        {
            return $""Hello World!"";
        }

        [FunctionName(""E1_SayHello_Tuple"")]
        public static string SayHelloTuple([ActivityTrigger] Tuple<string, int> tupleTest)
        {
            string name = tupleTest;
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_Tuple_OnContext"")]
        public static string SayHelloTupleOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<(string, int)>();
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_ArrayToNamed"")]
        public static string SayHelloTuple([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<IList<string>>();
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_NamedToNamed"")]
        public static string SayHelloTupleOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<IList<string>>();
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_NamedToNamed_Direct"")]
        public static string SayHelloTupleOnContext([ActivityTrigger] IList<string> namedType)
        {
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_NotUsed"")]
        public static string SayHelloTupleOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            return $""Hello {name}!"";
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Argument_Mismatch_IntAndString_OffContext()
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

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", 4));
            
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
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormat, "E1_SayHello", "string", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 84)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_Mismatch_IntAndString()
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

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", 4));
            
                return outputs;
            }

        [FunctionName(""E1_SayHello"")]
        public static string SayHello([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormat, "E1_SayHello", "string", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 84)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_Mismatch_StringArrayAndString_IEnumerableTypes()
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

                string[] arrayType = new string[] { ""Seattle"", ""Tokyo"" };
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", arrayType));
            
                return outputs;
            }

        [FunctionName(""E1_SayHello"")]
        public static string SayHello([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormat, "E1_SayHello", "string", "System.String[]"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 24, 84)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_InputNotUsed_NonNullInput()
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
        public static string SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormatNotUsed, "E1_SayHello"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 84)
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
