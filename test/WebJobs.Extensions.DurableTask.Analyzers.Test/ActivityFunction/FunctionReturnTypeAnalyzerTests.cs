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
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void ReturnType_NoDiagnosticTestCases()
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
        // Testing that no diagnostics are produced when method does not have the FunctionName attribute present
        public static async Task<string> NonFunctionWrongReturnType(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            // Incorrect return types on some test functions used below
            await context.CallActivityAsync<int>(""Test_String"", ""Tokyo"");
            await context.CallActivityAsync<int>(""Test_Object"", ""Tokyo"");
            await context.CallActivityAsync<int>(""Test_Array"", new string[] { ""Minneapolis"" });
            await context.CallActivityAsync<int>(""Test_Tuple"", (""Seattle"", 4));
            
            return ""Hello World!"";
        }

        [FunctionName(""ReturnTypeAnalyzerTestCases"")]
        public static async Task<string> Run(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Testing different matching return types
            // SyntaxKind.PredefinedType (string), SyntaxKind.IdentifierName (Object), and SyntaxKind.ArrayType (string[])

            await context.CallActivityAsync<string>(""Test_String"", ""Tokyo"");
            await context.CallActivityAsync<Object>(""Test_Object"", ""Tokyo"");
            await context.CallActivityAsync<string[]>(""Test_Array"", new string[] { ""Minneapolis"" });

            // SyntaxKind.GenericType (Tuple and ValueTuple) and SyntaxKind.TupleType (ValueTuple alt format ex (string, int))

            Tuple<string, int> tuple = new Tuple<string, int>(""Seattle"", 4);
            await context.CallActivityAsync<Tuple<string, int>>(""Test_Tuple"", tuple);
            await context.CallActivityAsync<(string, int)>(""Test_ValueTuple"", (""Seattle"", 4));
            await context.CallActivityAsync<ValueTuple<string, int>>(""Test_ValueTuple"", (""Seattle"", 4));
            await context.CallActivityAsync<ValueTuple<string, int>>(""Test_ValueTuple_AltFormat"", (""Seattle"", 4));
                
            // Testing JsonArray compatible types (IEnumerable Typles)
            // IArrayTypeSymbol (array) to INamedTypeSymbol (IList)
                
            await context.CallActivityAsync<string[]>(""Test_IList"", ""Seattle"");

            // INamedTypeSymbol (List) to INamedTypeSymbol (IList)
            await context.CallActivityAsync<List<string>>(""Test_IList"", ""London"");

            // Testing argument is valid when input is subclass (Object -> ValueType -> Char)

            await context.CallActivityAsync<Object>(""Test_ValueType"", ""Minneapolis"");
            await context.CallActivityAsync<Object>(""Test_Char"", ""Minneapolis"");

            // Task return types
            await context.CallActivityAsync<string>(""Test_TaskString"", ""London"");
            await context.CallActivityAsync<string[]>(""Test_TaskList"", ""London"");

            // Nullable types tests
            await context.CallActivityAsync<int?>(""Test_NullableInt"", null);
            await context.CallActivityAsync<int?>(""Test_GenericNullableInt"", null);
            
            // Testing no diagnostic on no specified return type
            await context.CallActivityAsync(""Test_String"", ""London"");

            return ""Hello World!"";
        }
        
        // Testing different matching return types
        // SyntaxKind.PredefinedType (string), SyntaxKind.IdentifierName (Object), and SyntaxKind.ArrayType (string[])

        [FunctionName(""Test_String"")]
        public static string TestString([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_Object"")]
        public static Object TestObject([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return new Object();
        }

        [FunctionName(""Test_Array"")]
        public static string[] TestArray([ActivityTrigger] string[] input)
        {
            return input;
        }

        // SyntaxKind.GenericType (Tuple and ValueTuple) and SyntaxKind.TupleType (ValueTuple alt format ex (string, int))

        [FunctionName(""Test_Tuple"")]
        public static Tuple<string, int> TestTuple([ActivityTrigger] Tuple<string, int> tuple)
        {
            return tuple;
        }

        [FunctionName(""Test_ValueTuple"")]
        public static ValueTuple<string, int> TestValueTuple([ActivityTrigger] ValueTuple<string, int> tuple)
        {
            return tuple;
        }

        [FunctionName(""Test_ValueTuple_AltFormat"")]
        public static (string, int) TestValueTupleAltFormat([ActivityTrigger] ValueTuple<string, int> tuple)
        {
            return tuple;
        }
        
        // Testing JsonArray compatible types (IEnumerable Typles)

        [FunctionName(""Test_IList"")]
        public static IList<string> TestIList([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return new List<string>();
        }

        // Testing argument is valid when input is subclass (Object -> ValueType -> Char)

        [FunctionName(""Test_ValueType"")]
        public static ValueType TestValueType([ActivityTrigger] string name)
        {
            return new Char();
        }

        [FunctionName(""Test_Char"")]
        public static Char TestChar([ActivityTrigger] string name)
        {
            return new Char();
        }

        // Task return types
        
        [FunctionName(""Test_TaskString"")]
        public static Task<string> TestTaskString([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_TaskList"")]
        public static Task<List<string>> TestTaskList([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return new List<string>();
        }
        
        // Nullable types tests

        [FunctionName(""Test_NullableInt"")]
        public static int? TestTaskList([ActivityTrigger] IDurableActivityContext context)
        {
            return 4;
        }

        [FunctionName(""Test_GenericNullableInt"")]
        public static Nullable<int> TestTaskList([ActivityTrigger] IDurableActivityContext context)
        {
            return 4;
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ReturnType_ExpectsInt_ReturnsString()
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
        [FunctionName(""ReturnTypeAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<int>(""Function_Returns_String"", ""test"");
            
                return ""Hello World!"";
            }

        [FunctionName(""Function_Returns_String"")]
        public static string FunctionReturnsString([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityReturnTypeAnalyzerMessageFormat, "Function_Returns_String", "string", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 23)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ReturnType_ExpectsString_ReturnsList()
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
        [FunctionName(""ReturnTypeAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function_Returns_ListOfString"", ""test"");
            
                return ""Hello World!"";
            }

        [FunctionName(""Function_Returns_ListOfString"")]
        public static List<string> FunctionReturnsListOfString([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return new List<string>() { name };
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityReturnTypeAnalyzerMessageFormat, "Function_Returns_ListOfString", "System.Collections.Generic.List<string>", "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 23)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void ReturnType_ExpectsString_ReturnsTask()
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
        [FunctionName(""ReturnTypeAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function_Returns_Task"", ""World"");
            
                return ""Hello World!"";
            }

        [FunctionName(""Function_Returns_Task"")]
        public static Task FunctionReturnsTask([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityReturnTypeAnalyzerMessageFormat, "Function_Returns_Task", "System.Threading.Tasks.Task", "string"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 23)
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
