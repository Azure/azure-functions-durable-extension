// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.ActivityFunction
{
    [TestClass]
    public class ArgumentAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = ArgumentAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = ArgumentAnalyzer.severity;

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
using Microsoft.Azure.WebJobs;
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

                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello"", ""Tokyo""));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_DirectInput"", ""London""));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple"", (""Seattle"", 4)));
                outputs.Add(await context.CallActivityAsync<string>(""E1_SayHello_Tuple_OnContext"", (""Seattle"", 4)));
            
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

        [FunctionName(""E1_SayHello_Tuple"")]
        public static string SayHello([ActivityTrigger] Tuple<string, int> tupleTest)
        {
            string name = tupleTest;
            return $""Hello {name}!"";
        }

        [FunctionName(""E1_SayHello_Tuple_OnContext"")]
        public static string SayHello([ActivityTrigger] IDurableActivityContext context)
        {
            string name = context.GetInput<(string, int)>();
            return $""Hello {name}!"";
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ClassName_NonIssue_NameOf()
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
        [FunctionName(nameof(HireEmployee))]
        public static async Task<Application> RunOrchestrator(
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ClassName_NonIssue_NameOf_Namespace()
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
        [FunctionName(nameof(ExternalInteraction.HireEmployee))]
        public static async Task<Application> RunOrchestrator(
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ClassName_Mismatch()
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
        [FunctionName(""HelloWorld"")]
        public static async Task<Application> RunOrchestrator(
            [EntityTrigger] IDurableEntityContext context,
            ILogger log)
            {
            }
}";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.EntityClassNameAnalyzerMessageFormat, "HireEmployee", "HelloWorld"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 25)
                     }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.EntityClassNameAnalyzerMessageFormat, "HireEmployee", "HelloWorld"),
                Severity = severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 10)
                     }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
            
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new FunctionAnalyzer();
        }
    }
}
