// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ArgumentAnalyzer
    {
        public const string DiagnosticId = "DF0108";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MismatchMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString InputNotUsedMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormatNotUsed), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor MismatchRule = new DiagnosticDescriptor(DiagnosticId, Title, MismatchMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor InputNotUsedRule = new DiagnosticDescriptor(DiagnosticId, Title, InputNotUsedMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static void ReportProblems(
            CompilationAnalysisContext context,
            SemanticModel semanticModel,
            IEnumerable<ActivityFunctionDefinition> functionDefinitions,
            IEnumerable<ActivityFunctionCall> functionInvocations)
        {
            foreach (var invocation in functionInvocations)
            {
                var definition = functionDefinitions.Where(x => x.FunctionName == invocation.FunctionName).FirstOrDefault();
                if (definition != null)
                {
                    var isInvokedWithNonNullInput = TryGetInvocationInputType(semanticModel, invocation, out ITypeSymbol invocationInputType);
                    var functionDefinitionUsesInput = TryGetDefinitionInputType(semanticModel, definition, out ITypeSymbol definitionInputType);

                    if (isInvokedWithNonNullInput && invocationInputType != null)
                    {
                        if (!functionDefinitionUsesInput)
                        {
                            var diagnostic = Diagnostic.Create(InputNotUsedRule, invocation.ArgumentNode.GetLocation(), invocation.FunctionName);

                            context.ReportDiagnostic(diagnostic);
                        }
                        else if (!IsValidArgumentForDefinition(invocationInputType, definitionInputType))
                        {
                            var diagnostic = Diagnostic.Create(MismatchRule, invocation.ArgumentNode.GetLocation(), invocation.FunctionName, definitionInputType.ToString(), invocationInputType.ToString());

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private static bool TryGetInvocationInputType(SemanticModel semanticModel, ActivityFunctionCall activityInvocation, out ITypeSymbol invocationInputType)
        {
            var activityInput = activityInvocation.ArgumentNode;
            if (activityInput == null)
            {
                invocationInputType = null;
                return false;
            }

            return SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, activityInput, out invocationInputType);
        }

        private static bool TryGetDefinitionInputType(SemanticModel semanticModel, ActivityFunctionDefinition functionDefinition, out ITypeSymbol definitionInputType)
        {
            var definitionInput = functionDefinition.ParameterNode;
            if (definitionInput == null)
            {
                definitionInputType = null;
                return false;
            }

            if (SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, definitionInput, out definitionInputType))
            {
                if (SyntaxNodeUtils.IsDurableActivityContext(definitionInputType))
                {
                    return TryGetInputTypeFromContext(semanticModel, definitionInput, out definitionInputType);
                }

                return true;
            }

            definitionInputType = null;
            return false;
        }

        private static bool TryGetInputTypeFromContext(SemanticModel semanticModel, SyntaxNode node, out ITypeSymbol definitionInputType)
        {
            if (TryGetDurableActivityContextExpression(semanticModel, node, out SyntaxNode durableContextExpression))
            {
                if (SyntaxNodeUtils.TryGetTypeArgumentIdentifier((MemberAccessExpressionSyntax)durableContextExpression, out SyntaxNode typeArgument))
                {
                    return SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, typeArgument, out definitionInputType);
                }
            }

            definitionInputType = null;
            return false;
        }

        private static bool TryGetDurableActivityContextExpression(SemanticModel semanticModel, SyntaxNode node, out SyntaxNode durableContextExpression)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(node, out SyntaxNode methodDeclaration))
            {
                var memberAccessExpressionList = methodDeclaration.DescendantNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                foreach (var memberAccessExpression in memberAccessExpressionList)
                {
                    var identifierName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName)).FirstOrDefault();
                    if (identifierName != null)
                    {
                        if (SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, identifierName, out ITypeSymbol typeSymbol))
                        {
                            if (SyntaxNodeUtils.IsDurableActivityContext(typeSymbol))
                            {
                                durableContextExpression = memberAccessExpression;
                                return true;
                            }
                        }
                    }
                }
            }

            durableContextExpression = null;
            return false;
        }

        private static bool IsValidArgumentForDefinition(ITypeSymbol invocationInputType, ITypeSymbol definitionInputType)
        {
            return SyntaxNodeUtils.IsMatchingDerivedOrCompatibleType(invocationInputType, definitionInputType);
        }
    }
}


