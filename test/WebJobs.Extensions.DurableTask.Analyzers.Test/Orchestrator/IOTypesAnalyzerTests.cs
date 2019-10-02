// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Orchestrator
{
    [TestClass]
    public class IOTypesAnalyzerTests : CodeFixVerifier
    {
        private readonly string diagnosticId = IOTypesAnalyzer.DiagnosticId;
        private readonly DiagnosticSeverity severity = IOTypesAnalyzer.severity;

        [TestMethod]
        public void NonIssueCalls()
        {
            var test = @"
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Data;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Table;
    using Microsoft.WindowsAzure.Storage.Queue;
    using Microsoft.Azure.Documents.Client;

    namespace VSSample
    {
        public static class NonIssueExample
        {
            public void iONonIssueCalls(HttpClient httpClient, SqlConnection sqlConnection, CloudBlobClient cloudBlobClient, CloudQueueClinet cloudQueueClinet, 
                CloudTableClient cloudTableClient, DocumentClient documentClient, WebRequest webRequest)
            {
                Uri uri = new Uri('test');

                HttpClient httpClient2;
                SqlConnection sqlConnection2;
                CloudBlobClient cloudBlobClient2;
                CloudQueueClinet cloudQueueClinet2;
                CloudTableClient cloudTableClient2;
                DocumentClient documentClient2;
                WebRequest webRequest2;

                httpClient.GetType();
                sqlConnection.GetType();
                cloudBlobClient.GetType();
                cloudQueueClient.GetType();
                cloudTableClient.GetType();
                documentClient.Dispose();
                webRequest.GetType();
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void HttpClientInOrchestrator_Class()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                HttpClient httpClient = new HttpClient();
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "HttpClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void HttpClientInOrchestrator_Varaible()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context, HttpClient httpClient)
            {
                httpClient.GetType();
            }
        }
    }";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "HttpClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 74)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "httpClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        [TestMethod]
        public void SqlConectionInOrchestrator_Class()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                SqlConnection sqlConnection;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "SqlConnection"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void SqlConnectionInOrchestrator_Varaible()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context, SqlConnection sqlConnection)
            {
                sqlConnection.GetType();
            }
        }
    }";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "SqlConnection"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 74)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "sqlConnection"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        [TestMethod]
        public void CloudBlobClientInOrchestrator_Class()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                CloudBlobClient cloudBlobClient;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "CloudBlobClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void CloudBlobClientInOrchestrator_Varaible()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context, CloudBlobClient cloudBlobClient)
            {
                cloudBlobClient.GetType();
            }
        }
    }";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "CloudBlobClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 74)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "cloudBlobClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        [TestMethod]
        public void CloudQueueClientInOrchestrator_Class()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                CloudQueueClient cloudQueueClient;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "CloudQueueClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void CloudQueueClientInOrchestrator_Varaible()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context, CloudQueueClient cloudQueueClient)
            {
                cloudQueueClient.GetType();
            }
        }
    }";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "CloudQueueClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 74)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "cloudQueueClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        [TestMethod]
        public void CloudTableClientInOrchestrator_Class()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                CloudTableClient cloudTableClient;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "CloudTableClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void CloudTableClientInOrchestrator_Varaible()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context, CloudTableClient cloudTableClient)
            {
                cloudTableClient.GetType();
            }
        }
    }";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "CloudTableClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 74)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "cloudTableClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        [TestMethod]
        public void DocumentClientInOrchestrator_Class()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                DocumentClient documentClient;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "DocumentClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void DocumentClienttInOrchestrator_Varaible()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context, DocumentClient documentClient)
            {
                documentClient.Dispose();
            }
        }
    }";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "DocumentClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 74)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "documentClient"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        [TestMethod]
        public void WebRequestInOrchestrator_Class()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
            {
                WebRequest webRequest;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "WebRequest"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void WebRequesttInOrchestrator_Varaible()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;

    namespace VSSample
    {
        public static class HelloSequence
        {
            [FunctionName('E1_HelloSequence')]
            public static async Task<List<string>> Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context, WebRequest webRequest)
            {
                webRequest.GetType();
            }
        }
    }";
            var expectedResults = new DiagnosticResult[2];
            expectedResults[0] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "WebRequest"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 13, 74)
                        }
            };

            expectedResults[1] = new DiagnosticResult
            {
                Id = diagnosticId,
                Message = String.Format(Resources.DeterministicAnalyzerMessageFormat, "webRequest"),
                Severity = severity,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 17)
                        }
            };

            VerifyCSharpDiagnostic(test, expectedResults);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new IOTypesAnalyzer();
        }
    }
}
