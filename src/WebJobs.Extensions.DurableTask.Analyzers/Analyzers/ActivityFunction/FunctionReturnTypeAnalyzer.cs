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
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);


        public static void ReportProblems(
            CompilationAnalysisContext context,
            IEnumerable<ActivityFunctionDefinition> functionDefinitions,
            IEnumerable<ActivityFunctionCall> functionInvocations)
        {
            foreach (var invocation in functionInvocations)
            {
                var definition = functionDefinitions.FirstOrDefault(x => x.FunctionName == invocation.FunctionName);
                if (definition != null && invocation.ReturnTypeNode != null)
                {
                    if (!IsValidReturnTypeForDefinition(invocation, definition))
                    {
                        var diagnostic = Diagnostic.Create(Rule, invocation.InvocationExpression.GetLocation(), invocation.FunctionName, definition.ReturnType, invocation.ReturnType);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsValidReturnTypeForDefinition(ActivityFunctionCall invocation, ActivityFunctionDefinition definition)
        {
            var definitionReturnType = definition.ReturnType;
            if (TryGetTaskTypeArgument(definitionReturnType, out ITypeSymbol taskTypeArgument))
            {
                definitionReturnType = taskTypeArgument;
            }

            return SyntaxNodeUtils.IsMatchingDerivedOrCompatibleType(definitionReturnType, invocation.ReturnType);
        }

        private static bool TryGetTaskTypeArgument(ITypeSymbol returnType, out ITypeSymbol taskTypeArgument)
        {
            if (returnType is INamedTypeSymbol namedType && returnType.Name.Equals("Task"))
            {
                taskTypeArgument = namedType.TypeArguments.FirstOrDefault();
                return taskTypeArgument != null;
            }

            taskTypeArgument = null;
            return false;
        }
    }
}
