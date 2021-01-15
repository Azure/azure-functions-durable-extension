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
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void Argument_NoDiagnosticTestCases()
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
        public static async Task<string> NotInsideFunctionWrongInput(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                // Incorrect inputs on some test functions used below
                await context.CallActivityAsync<string>(""Test_String_DirectInput"", 100);
                await context.CallActivityAsync<string>(""Test_String_OnContext"", 100);
                await context.CallActivityAsync<string>(""Test_Object_DirectInput"", 100);
                await context.CallActivityAsync<string>(""Test_Object_OnContext"", 100);
            
                return ""Hello World!"";
            }

        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                // For a test case below
                var (jobId, batchNumber) = context.GetInput<(string, int)>();

                // Testing different matching input types
                // SyntaxKind.PredefinedType (string), SyntaxKind.IdentifierName (Object), and SyntaxKind.ArrayType (string[])

                await context.CallActivityAsync<string>(""Test_String_DirectInput"", ""London"");
                await context.CallActivityAsync<string>(""Test_String_OnContext"", ""Tokyo"");
                await context.CallActivityAsync<string>(""Test_Object_DirectInput"", new Object());
                await context.CallActivityAsync<string>(""Test_Object_OnContext"", new Object());
                await context.CallActivityAsync<string>(""Test_StringArray_DirectInput"", new string[] { ""Minneapolis"" });
                await context.CallActivityAsync<string>(""Test_StringArray_OnContext"", new string[] { ""Minneapolis"" });

                // SyntaxKind.GenericType (Tuple and ValueTuple) and SyntaxKind.TupleType (ValueTuple alt format ex (string, int))

                Tuple<string, int> tuple = new Tuple<string, int>(""Seattle"", 4);
                await context.CallActivityAsync<string>(""Test_Tuple_DirectInput"", tuple);
                await context.CallActivityAsync<string>(""Test_Tuple_OnContext"", tuple);
                await context.CallActivityAsync<string>(""Test_ValueTuple_DirectInput"", (""Seattle"", 4));
                await context.CallActivityAsync<string>(""Test_ValueTuple_OnContext"", (""Seattle"", 4));
                await context.CallActivityAsync<string>(""Test_ValueTuple_VariableNames"", (jobId, batchNumber));

                // Testing JsonArray compatible types (IEnumerable Typles)
                // IArrayTypeSymbol (array) to INamedTypeSymbol (IList)

                await context.CallActivityAsync<string>(""Test_IListInput_DirectInput"", new string[] { ""Seattle"" });
                await context.CallActivityAsync<string>(""Test_IListInput_OnContext"", new string[] { ""Seattle"" });
                
                // INamedTypeSymbol (List) and INamedTypeSymbol (Ilist)

                List<string> namedType = new List<string>();
                await context.CallActivityAsync<string>(""Test_IListInput_DirectInput"", namedType);
                await context.CallActivityAsync<string>(""Test_IListInput_OnContext"", namedType);

                // Testing argument is valid when input is subclass (Object -> ValueType -> Char)

                await context.CallActivityAsync<string>(""Test_ValueType_DirectInput"", new Char());
                await context.CallActivityAsync<string>(""Test_Object_DirectInput"", new Char());

                // Testing null input when function input not used

                await context.CallActivityAsync<string>(""Test_UnusedInputFromContext"", null);

                // Testing null input used with non value type

                await context.CallActivityAsync(""Test_ValidNull"", null);
            
                return ""Hello World!"";

                // Nightmare Test; testing nested Tuples, JSON compatible types, indirect subclass, getting input on a context

                await context.CallActivityAsync<string>(""Test_NightmareTest"", new List<Tuple<(int, IEnumerable<string>), Decimal>>());
            }

        // Functions Testing different matching input types
        // SyntaxKind.PredefinedType (string), SyntaxKind.IdentifierName (Object), and SyntaxKind.ArrayType (string[])

        [FunctionName(""Test_String_DirectInput"")]
        public static string TestStringDirectInput([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_String_OnContext"")]
        public static string TestStringOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_Object_DirectInput"")]
        public static string TestObjectDirectInput([ActivityTrigger] Object name)
        {
            return $""Hello World!"";
        }

        [FunctionName(""Test_Object_OnContext"")]
        public static string TestObjectOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<Object>();
            return $""Hello World!"";
        }

        [FunctionName(""Test_StringArray_DirectInput"")]
        public static string TestStringArrayDirectInput([ActivityTrigger] string[] names)
        {
            return $""Hello World!"";
        }

        [FunctionName(""Test_StringArray_OnContext"")]
        public static string TestStringArrayOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string[]>();
            return $""Hello World!"";
        }

        // SyntaxKind.GenericType (Tuple and ValueTuple) and SyntaxKind.TupleType (ValueTuple alt format ex (string, int))

        [FunctionName(""Test_Tuple_DirectInput"")]
        public static string TestTupleDirectInput([ActivityTrigger] Tuple<string, int> tupleTest)
        {
            string name = tupleTest;
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_Tuple_OnContext"")]
        public static string TestTupleOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<Tuple<string, int>>();
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_ValueTuple_DirectInput"")]
        public static string TestValueTupleDirectInput([ActivityTrigger] ValueTuple<string, int> tupleTest)
        {
            string name = tupleTest;
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_ValueTuple_OnContext"")]
        public static string TestValueTupleOnContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<(string, int)>();
            return $""Hello {name}!"";
        }

        [FunctionName(""Test_ValueTuple_VariableNames"")]
        public static string TestValueTupleVariableNames([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<(string, int)>();
            return $""Hello {name}!"";
        }

        // Testing JsonArray compatible types (IEnumerable Typles)

        [FunctionName(""Test_IListInput_DirectInput"")]
        public static string TestIListInputDirectInput([ActivityTrigger] IList<string> namedType)
        {
            return $""Hello {name}!"";
        

        [FunctionName(""Test_IListInput_OnContext"")]
        public static string TestIListInputContext([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<IList<string>>();
            return $""Hello {name}!"";
        }

        // Testing argument is valid when input is subclass (Object -> ValueType -> Char)
         [FunctionName(""Test_ValueType_DirectInput"")]
        public static string TestValueTypeDirectInput([ActivityTrigger] ValueType input)
        {
            return $""Hello World!"";
        }

        // Testing null input when function input not used

        [FunctionName(""Test_UnusedInputFromContext"")]
        public static string TestUnusedInputFromContext([ActivityTrigger] IDurableActivityContext context)
        {
            return $""Hello {name}!"";
        }

        // Testing null input used with non value type

        [FunctionName(""Test_ValidNull"")]
        public static string TestUnusedInputFromContext([ActivityTrigger] int? name)
        {
            return $""Hello {name}!"";
        }

        // Nightmare Test; testing nested Tuples, JSON compatible types, indirect subclass, getting input on a context

        [FunctionName(""Test_NightmareTest"")]
        public static string TestNightmareTest([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<Tuple<(int, List<string>), Object>[]>();
            return $""Hello World!"";
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Argument_NoDiagnosticTestCases_CustomMethods()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace VSSample
{
    public static class HelloSequence
    {
        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var retryOptions = new RetryOptions(new TimeSpan(), 2);

                // Test correct custom method is used to determine which argument the input is.
                await context.CallActivityAsync<string>(""Extension_InputNotLast"", ""input"", retryOptions);

                // Test argument analyzer does not compare input on default parameter.
                await context.CallActivityAsync<string>(""Extension_InputNotLast"", ""notInput"", 2, 3, retryOptions: retryOptions);
            }

        [FunctionName(""Extension_InputNotLast"")]
        public static string ExtensionInputNotLast([ActivityTrigger] string name)
            {
                return $""Hello {name}!"";
            }

            // Test correct custom method is used to determine which argument the input is

            public static Task<TResult> CallActivityAsync<TResult>(
            this IDurableOrchestrationContext context, string functionName, object input, RetryOptions retryOptions)
            {
                return retryOptions != null
                    ? context.CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input)
                    : context.CallActivityAsync<TResult>(functionName, input);
            }

            public static Task<TResult> CallActivityAsync<TResult>(
            this IDurableOrchestrationContext context, string functionName, int integer, object input, RetryOptions retryOptions)
            {
                return retryOptions != null
                    ? context.CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input)
                    : context.CallActivityAsync<TResult>(functionName, input);
            }

            // Test argument analyzer does not compare input on default parameter

            public static Task<TResult> CallActivityAsync<TResult>(
            this IDurableOrchestrationContext context, string functionName, string stringOne, int integerOne, int integerTwo, object input = null, RetryOptions retryOptions = null)
            {
                return retryOptions != null
                    ? context.CallActivityWithRetryAsync<TResult>(functionName, retryOptions, input)
                    : context.CallActivityAsync<TResult>(functionName, input);
            }
        }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Argument_GivenInt_TakesString_OffContext()
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
        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function_Takes_String"", 4);
            
                return ""Hello World!"";
            }

        [FunctionName(""Function_Takes_String"")]
        public static string FunctionTakesString([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormat, "Function_Takes_String", "string", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 82)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_GivenInt_TakesString_DirectInput()
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
        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Function_Takes_String"", 4);
            
                return ""Hello World!"";
            }

        [FunctionName(""Function_Takes_String"")]
        public static string FunctionTakesString([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormat, "Function_Takes_String", "string", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 82)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_GivenArray_TakesString_DirectInput()
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
        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                string[] arrayType = new string[] { ""Seattle"", ""Tokyo"" };
                await context.CallActivityAsync<string>(""Function_Takes_String"", arrayType);
            
                return ""Hello World!"";
            }

        [FunctionName(""Function_Takes_String"")]
        public static string FunctionTakesString([ActivityTrigger] string name)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormat, "Function_Takes_String", "string", "string[]"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 22, 82)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_InputNotUsedOnContext_NonNullInput()
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
        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""Unused_Input"", ""World""));
            
                return ""Hello World!"";
            }

        [FunctionName(""Unused_Input"")]
        public static string UnusedInput([ActivityTrigger] IDurableActivityContext context)
        {
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormatNotUsed, "Unused_Input"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 73)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_InputNullWithValueType()
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
        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""InvalidNullInput"", null));
            
                return ""Hello World!"";
            }

        [FunctionName(""InvalidNullInput"")]
        public static string InvalidNullInput([ActivityTrigger] int input)
        {
            return $""Hello World!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormatInvalidNull, "InvalidNullInput", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 77)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Argument_InputNullWithValueType2()
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
        [FunctionName(""ArgumentAnalyzerTestCases"")]
        public static async Task<string> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                await context.CallActivityAsync<string>(""InvalidNullInput"", null));
            
                return ""Hello World!"";
            }

        [FunctionName(""InvalidNullInput"")]
        public static string InvalidNullInput([ActivityTrigger] int input)
        {
            return $""Hello World!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityArgumentAnalyzerMessageFormatInvalidNull, "InvalidNullInput", "int"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 77)
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
