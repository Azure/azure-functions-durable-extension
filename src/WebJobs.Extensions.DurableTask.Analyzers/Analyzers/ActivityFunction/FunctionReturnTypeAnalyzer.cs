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
            IEnumerable<ActivityFunctionDefinition> availableFunctions, 
            IEnumerable<ActivityFunctionCall> calledFunctions)
        {
            foreach (var activityInvocation in calledFunctions)
            {
                var functionDefinition = availableFunctions.Where(x => x.FunctionName == activityInvocation.Name).FirstOrDefault();
                if (functionDefinition != null && activityInvocation.ReturnTypeNode != null)
                {
                    TryGetInvocationReturnType(semanticModel, activityInvocation, out ITypeSymbol invocationReturnType);
                    TryGetDefinitionReturnType(semanticModel, functionDefinition, out ITypeSymbol definitionReturnType);

                    if (!InputMatchesOrTaskOrCompatibleType(invocationReturnType, definitionReturnType))
                    {
                        var invocationTypeName = SyntaxNodeUtils.GetQualifiedTypeName(invocationReturnType);
                        var functionTypeName = SyntaxNodeUtils.GetQualifiedTypeName(definitionReturnType);

                        var diagnostic = Diagnostic.Create(Rule, activityInvocation.InvocationExpression.GetLocation(), activityInvocation.Name, functionTypeName, invocationTypeName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool InputMatchesOrTaskOrCompatibleType(ITypeSymbol invocationReturnType, ITypeSymbol definitionReturnType)
        {
            return SyntaxNodeUtils.InputMatchesOrCompatibleType(invocationReturnType, definitionReturnType) || DefinitionReturnsTask(invocationReturnType, definitionReturnType);
        }

        private static bool DefinitionReturnsTask(ITypeSymbol invocationReturnType, ITypeSymbol definitionReturnType)
        {
            if (definitionReturnType.Name.Equals("Task") && definitionReturnType is INamedTypeSymbol namedType)
            {
                var taskTypeArgument = namedType.TypeArguments.FirstOrDefault();
                if (taskTypeArgument != null)
                {
                    return invocationReturnType.Equals(taskTypeArgument);
                }
            }

            return false;
        }

        private static void TryGetInvocationReturnType(SemanticModel semanticModel, ActivityFunctionCall activityInvocation, out ITypeSymbol invocationReturnType)
        {
            var invocationReturnNode = activityInvocation.ReturnTypeNode;

            invocationReturnType = SyntaxNodeUtils.GetSyntaxTreeSemanticModel(semanticModel, invocationReturnNode).GetTypeInfo(invocationReturnNode).Type;
        }

        private static void TryGetDefinitionReturnType(SemanticModel semanticModel, ActivityFunctionDefinition functionDefinition, out ITypeSymbol definitionReturnType)
        {
            var definitionReturnNode = functionDefinition.ReturnTypeNode;

            definitionReturnType = SyntaxNodeUtils.GetSyntaxTreeSemanticModel(semanticModel, definitionReturnNode).GetTypeInfo(definitionReturnNode).Type;
        }
    }
}
