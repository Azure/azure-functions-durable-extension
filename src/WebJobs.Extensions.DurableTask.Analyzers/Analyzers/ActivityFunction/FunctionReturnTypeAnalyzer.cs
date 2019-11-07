// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class FunctionReturnTypeAnalyzer
    {
        public const string DiagnosticId = "DF0110";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityReturnTypeAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ActivityReturnTypeAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityReturnTypeAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);


        public void ReportProblems(CompilationAnalysisContext cac, IEnumerable<ActivityFunctionDefinition> availableFunctions, IEnumerable<ActivityFunctionCall> calledFunctions)
        {
            foreach (var node in calledFunctions)
            {
                var functionDefinition = availableFunctions.Where(x => x.FunctionName == node.Name).SingleOrDefault();
                if (functionDefinition != null)
                {
                    // Functions can always return Task, regardless of function definition return type
                    if (functionDefinition.ReturnType != node.ExpectedReturnType &&
                        node.ExpectedReturnType != "System.Threading.Tasks.Task")
                    {
                        if ($"System.Threading.Tasks.Task<{functionDefinition.ReturnType}>" != node.ExpectedReturnType)
                            cac.ReportDiagnostic(Diagnostic.Create(Rule, node.InvocationExpression.GetLocation(), node.Name, functionDefinition.ReturnType, node.ExpectedReturnType));
                    }
                }
            }
        }
    }
}
