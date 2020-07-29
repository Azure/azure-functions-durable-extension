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
            SemanticModel semanticModel,
            IEnumerable<ActivityFunctionDefinition> functionDefinitions,
            IEnumerable<ActivityFunctionCall> functionInvocations)
        {
            foreach (var invocation in functionInvocations)
            {
                var definition = functionDefinitions.Where(x => x.FunctionName == invocation.FunctionName).FirstOrDefault();
                if (definition != null && invocation.ReturnTypeNode != null)
                {
                    if (TryGetInvocationReturnType(semanticModel, invocation, out ITypeSymbol invocationReturnType)
                        && TryGetDefinitionReturnType(semanticModel, definition, out ITypeSymbol definitionReturnType))
                    {
                        if (!IsValidReturnTypeForDefinition(invocationReturnType, definitionReturnType))
                        {
                            var diagnostic = Diagnostic.Create(Rule, invocation.InvocationExpression.GetLocation(), invocation.FunctionName, definitionReturnType.ToString(), invocationReturnType.ToString());

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private static bool IsValidReturnTypeForDefinition(ITypeSymbol invocationReturnType, ITypeSymbol definitionReturnType)
        {
            if (TryGetTaskTypeArgument(definitionReturnType, out ITypeSymbol taskTypeArgument))
            {
                definitionReturnType = taskTypeArgument;
            }

            return SyntaxNodeUtils.IsMatchingDerivedOrCompatibleType(definitionReturnType, invocationReturnType);
        }

        private static bool TryGetTaskTypeArgument(ITypeSymbol returnType, out ITypeSymbol taskTypeArgument)
        {
            if (returnType.Name.Equals("Task") && returnType is INamedTypeSymbol namedType)
            {
                taskTypeArgument = namedType.TypeArguments.FirstOrDefault();
                return taskTypeArgument != null;
            }

            taskTypeArgument = null;
            return false;
        }

        private static bool TryGetInvocationReturnType(SemanticModel semanticModel, ActivityFunctionCall activityInvocation, out ITypeSymbol invocationReturnType)
        {
            var invocationReturnNode = activityInvocation.ReturnTypeNode;

            return SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, invocationReturnNode, out invocationReturnType);
        }

        private static bool TryGetDefinitionReturnType(SemanticModel semanticModel, ActivityFunctionDefinition functionDefinition, out ITypeSymbol definitionReturnType)
        {
            var definitionReturnNode = functionDefinition.ReturnTypeNode;

            return SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, definitionReturnNode, out definitionReturnType);
        }
    }
}
