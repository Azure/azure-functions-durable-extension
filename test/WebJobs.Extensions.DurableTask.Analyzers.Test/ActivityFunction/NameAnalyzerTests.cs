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
        private static readonly DiagnosticSeverity Severity = NameAnalyzer.Severity;

        [TestMethod]
        public void Name_NonIssueCalls()
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
        public const string FunctionName = ""SayHelloByConstFuncName"";
        public const string FunctionNameWithClass = ""SayHelloByConstFuncNameWithClass"";

        // Should not flag code on non function
        public static async Task<List<string>> NonFunctionInvalidNames(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                outputs.Add(await context.CallActivityAsync<string>(""NotAFunction"", ""Tokyo""));
                outputs.Add(await context.CallActivityAsync<string>(""DefinitelyNotAFunction"", new Object()));
            
                return outputs;
            }

        [FunctionName(""E1_HelloSequence"")]
        public static async Task<List<string>> CorrectNames(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                // Matching names
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""Tokyo""));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_DirectInput"", ""London""));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Object"", new Object()));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Object_DirectInput"", new Object()));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple"", (""Seattle"", 4)));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple_OnContext"", (""Seattle"", 4)));
                outputs.Add(await context.CallActivityAsync<string>(nameof(SayHelloByClassName), ""Amsterdam""));
                outputs.Add(await context.CallActivityAsync<string>(""SayHelloByMethodName"", ""Amsterdam""));
                outputs.Add(await context.CallActivityAsync<string>(nameof(SayHelloByMethodName), ""Amsterdam""));
                outputs.Add(await context.CallActivityAsync<string>(""SayHelloByConstFuncName"", ""Amsterdam""));
                outputs.Add(await context.CallActivityAsync<string>(""SayHelloByConstFuncNameWithClass"", ""Amsterdam""));
                outputs.Add(await context.CallActivityAsync<string>(constantSayHelloByConstFuncName, ""Amsterdam""));
                outputs.Add(await context.CallActivityAsync<string>(HelloSequence.constantSayHelloByConstFuncNameWithClass, ""Amsterdam""));

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

        [FunctionName(nameof(SayHelloByMethodName))]
        public static string SayHelloByMethodName([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        
        //constant variable used as functionName
        [FunctionName(FunctionName)]
        public static string SayHelloByMethodName([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }

        [FunctionName(HelloSequence.FunctionNameWithClass)]
        public static string SayHelloByMethodName([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }

    public class SayHelloByClassName
    {
        [FunctionName(nameof(SayHelloByClassName))]
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
        public void Name_InvalidFunctionName_Close()
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

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHey"", 4));
            
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
                Message = string.Format(Resources.ActivityNameAnalyzerCloseMessageFormat, "E1_SayHey", "E1_SayHello"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 69)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Name_InvalidFunctionName_NameOfClassDoesNotMatchFunctionName()
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
    public class HelloSequence
    {
        [FunctionName(""E1_HelloSequence"")]
        public async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), 4));
            
                return outputs;
            }
    }

    public class SayHello
    {
        [FunctionName(""SayHi"")]
        public string Run([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<string>();
            return $""Hello {name}!"";
        }
    }
}";
            var expectedDiagnostics = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.ActivityNameAnalyzerCloseMessageFormat, "SayHello", "SayHi"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 69)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Name_InvalidFunctionName_Missing()
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

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""Ben""));
            
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
                            new DiagnosticResultLocation("Test0.cs", 23, 69)
                     }
            };
            VerifyCSharpDiagnostic(test, expectedDiagnostics);
        }

        [TestMethod]
        public void Name_InvalidFunctionName_Missing_UsingConst()
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

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""Ben""));
            
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

        [TestMethod]
        public void Name_InvalidFunctionName_Missing_UsingConstAndClass()
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
    public static class Consts
    {
        public const string FunctionName = ""E1_HelloSequence"";
    }

    public static class HelloSequence
    {
        [FunctionName(Consts.FunctionName)]
        public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                var outputs = new List<string>();

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""Ben""));
            
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
                            new DiagnosticResultLocation("Test0.cs", 28, 69)
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
