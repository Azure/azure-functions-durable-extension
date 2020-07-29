// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers.Test.Entity
{
    [TestClass]
    public class ClassNameAnalyzerTests : CodeFixVerifier
    {
        private static readonly string DiagnosticId = ClassNameAnalyzer.DiagnosticId;
        private static readonly DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        [TestMethod]
        public void ClassName_NoDiagnosticMatch()
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
        [FunctionName(""HelloSequence"")]
        public static async Task Run(
            [EntityTrigger] IDurableEntityContext context)
            {
            }
}";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ClassName_NoDiagnosticNameOf()
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
        [FunctionName(nameof(HelloSequence))]
        public static async Task Run(
            [EntityTrigger] IDurableEntityContext context)
            {
            }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ClassName_NoDiagnosticNameOfWithNamespace()
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
        [FunctionName(nameof(VSSample.HelloSequence))]
        public static async Task Run(
            [EntityTrigger] IDurableEntityContext context)
            {
            }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ClasName_Mismatch()
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
        [FunctionName(""HelloWorld"")]
        public static async Task Run(
            [EntityTrigger] IDurableEntityContext context)
            {
            }
}";

            var expectedResults = new DiagnosticResult
            {
                Id = DiagnosticId,
                Message = string.Format(Resources.EntityClassNameAnalyzerCloseMessageFormat, "HelloWorld", "HelloSequence"),
                Severity = Severity,
                Locations =
                 new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 23)
                     }
            };
            
            VerifyCSharpDiagnostic(test, expectedResults);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ClassNameAnalyzer();
        }
    }
}
