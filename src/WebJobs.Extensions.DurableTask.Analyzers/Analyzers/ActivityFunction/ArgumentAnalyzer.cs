﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ArgumentAnalyzer
    {
        public const string DiagnosticId = "DF0108";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString InputNotUsedMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormatNotUsed), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor InputNotUsedRule = new DiagnosticDescriptor(DiagnosticId, Title, InputNotUsedMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static void ReportProblems(CompilationAnalysisContext context, SemanticModel semanticModel, IEnumerable<ActivityFunctionDefinition> availableFunctions, IEnumerable<ActivityFunctionCall> calledFunctions)
        {
            foreach (var activityInvocation in calledFunctions)
            {
                var functionDefinition = availableFunctions.Where(x => x.FunctionName == activityInvocation.Name).SingleOrDefault();
                if (functionDefinition != null)
                {
                    TryGetInvocationInputType(semanticModel, activityInvocation, out ITypeSymbol invocationInputType);
                    TryGetDefinitionInputType(semanticModel, functionDefinition, out ITypeSymbol definitionInputType);

                    var invocationTypeName = SyntaxNodeUtils.GetQualifiedTypeName(invocationInputType);
                    var definitionTypeName = SyntaxNodeUtils.GetQualifiedTypeName(definitionInputType);

                    if (definitionInputType == null)
                    {
                        var diagnostic = Diagnostic.Create(InputNotUsedRule, activityInvocation.ParameterNode.GetLocation(), activityInvocation.Name);

                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (!SyntaxNodeUtils.InputMatchesOrCompatibleType(invocationInputType, invocationTypeName, definitionInputType, definitionTypeName))
                    {
                        var diagnostic = Diagnostic.Create(Rule, activityInvocation.ParameterNode.GetLocation(), activityInvocation.Name, definitionTypeName, invocationTypeName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static void TryGetInvocationInputType(SemanticModel semanticModel, ActivityFunctionCall activityInvocation, out ITypeSymbol invocationInputType)
        {
            var invocationInput = activityInvocation.ParameterNode;

            invocationInputType = SyntaxNodeUtils.GetSyntaxTreeSemanticModel(semanticModel, invocationInput).GetTypeInfo(invocationInput).Type;
        }

        private static void TryGetDefinitionInputType(SemanticModel semanticModel, ActivityFunctionDefinition functionDefinition, out ITypeSymbol definitionInputType)
        {
            var definitionInput = functionDefinition.ParameterNode;

            if (FunctionParameterIsContext(semanticModel, definitionInput))
            {
                if (!TryGetInputFromDurableContextCall(semanticModel, definitionInput, out definitionInput))
                {
                    definitionInputType = null;
                    return;
                }
            }

            definitionInputType = SyntaxNodeUtils.GetSyntaxTreeSemanticModel(semanticModel, definitionInput).GetTypeInfo(definitionInput).Type;
        }

        private static bool FunctionParameterIsContext(SemanticModel semanticModel, SyntaxNode functionInput)
        {
            var parameterTypeName = SyntaxNodeUtils.GetSyntaxTreeSemanticModel(semanticModel, functionInput).GetTypeInfo(functionInput).Type.ToString();

            return (parameterTypeName.Equals("Microsoft.Azure.WebJobs.Extensions.DurableTask.IDurableActivityContext")
            || parameterTypeName.Equals("Microsoft.Azure.WebJobs.DurableActivityContext")
            || parameterTypeName.Equals("Microsoft.Azure.WebJobs.DurableActivityContextBase"));
        }

        private static bool TryGetInputFromDurableContextCall(SemanticModel semanticModel, SyntaxNode definitionInput, out SyntaxNode inputFromContext)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(definitionInput, out SyntaxNode methodDeclaration))
            {
                var memberAccessExpressionList = methodDeclaration.DescendantNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                foreach (var memberAccessExpression in memberAccessExpressionList)
                {
                    var identifierName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName)).FirstOrDefault();
                    if (identifierName != null)
                    {
                        var identifierNameType = SyntaxNodeUtils.GetSyntaxTreeSemanticModel(semanticModel, identifierName).GetTypeInfo(identifierName).Type.Name;
                        if (identifierNameType.Equals("IDurableActivityContext") || identifierNameType.Equals("DurableActivityContext"))
                        {
                            var genericName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName)).FirstOrDefault();
                            if (genericName != null)
                            {
                                var typeArgumentList = genericName.ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList)).FirstOrDefault();
                                if (typeArgumentList != null)
                                {
                                    inputFromContext = typeArgumentList.ChildNodes().First();
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            inputFromContext = null;
            return false;
        }
    }
}


