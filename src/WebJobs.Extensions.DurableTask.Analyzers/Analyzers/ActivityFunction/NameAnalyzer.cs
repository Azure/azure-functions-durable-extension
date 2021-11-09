// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class NameAnalyzer
    {
        public const string DiagnosticId = "DF0109";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString CloseMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerCloseMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MissingMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerMissingMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor CloseRule = new DiagnosticDescriptor(DiagnosticId, Title, CloseMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor MissingRule = new DiagnosticDescriptor(DiagnosticId, Title, MissingMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static void ReportProblems(
            CompilationAnalysisContext context,
            SemanticModel semanticModel,
            IEnumerable<ActivityFunctionDefinition> functionDefinitions,
            IEnumerable<ActivityFunctionCall> functionInvocations)
        {
            foreach (var invocation in functionInvocations)
            {
                // If invocation uses constant and there is no matching function name in function definition, trust the customer for correctness in case they are using 
                // <FunctionsInDependencies>true</FunctionsInDependencies>
                if (!functionDefinitions.Select(x => x.FunctionName).Contains(invocation.FunctionName) && !IsConstant(semanticModel, invocation.NameNode))
                {
                    if (SyntaxNodeUtils.TryGetClosestString(invocation.FunctionName, functionDefinitions.Select(x => x.FunctionName), out string closestName))
                    {
                        var diagnostic = Diagnostic.Create(CloseRule, invocation.NameNode.GetLocation(), invocation.FunctionName, closestName);

                        context.ReportDiagnostic(diagnostic);
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(MissingRule, invocation.NameNode.GetLocation(), invocation.FunctionName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsConstant(SemanticModel semanticModel, SyntaxNode nameNode)
        {
            return SyntaxNodeUtils.TryGetFunctionNameInConstant(semanticModel, nameNode, out _);
        }
    }
}
