// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FunctionAnalyzer : DiagnosticAnalyzer
    {
        private List<ActivityFunctionDefinition> availableFunctions = new List<ActivityFunctionDefinition>();
        private List<ActivityFunctionCall> calledFunctions = new List<ActivityFunctionCall>();
        private SemanticModel semanticModel;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    NameAnalyzer.MissingRule,
                    NameAnalyzer.CloseRule,
                    ArgumentAnalyzer.MismatchRule,
                    FunctionReturnTypeAnalyzer.Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            FunctionAnalyzer functionAnalyzer = new FunctionAnalyzer();
            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.FindActivityCall, SyntaxKind.InvocationExpression);
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.FindActivityFunction, SyntaxKind.Attribute);

                compilation.RegisterCompilationEndAction(functionAnalyzer.RegisterAnalyzers);
            });
        }

        private void RegisterAnalyzers(CompilationAnalysisContext context)
        {
            NameAnalyzer.ReportProblems(context, semanticModel, availableFunctions, calledFunctions);
            ArgumentAnalyzer.ReportProblems(context, semanticModel, availableFunctions, calledFunctions);
            FunctionReturnTypeAnalyzer.ReportProblems(context, semanticModel, availableFunctions, calledFunctions);
        }

        public void FindActivityCall(SyntaxNodeAnalysisContext context)
        {
            SetSemanticModel(context);

            var semanticModel = context.SemanticModel;
            if (context.Node is InvocationExpressionSyntax invocationExpression
                && SyntaxNodeUtils.IsInsideFunction(semanticModel, invocationExpression)
                && IsActivityInvocation(invocationExpression))
            {
                if (!TryGetFunctionNameFromActivityInvocation(invocationExpression, out SyntaxNode functionNameNode, out string functionName))
                {
                    //Do not store ActivityFunctionCall if there is no function name
                    return;
                }

                SyntaxNodeUtils.TryGetTypeArgumentIdentifier((MemberAccessExpressionSyntax)invocationExpression.Expression, out SyntaxNode returnTypeNode);

                SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, returnTypeNode, out ITypeSymbol returnType);

                TryGetInputNodeFromCallActivityInvocation(invocationExpression, out SyntaxNode inputNode);

                SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, inputNode, out ITypeSymbol inputType);

                calledFunctions.Add(new ActivityFunctionCall
                {
                    FunctionName = functionName,
                    NameNode = functionNameNode,
                    InputNode = inputNode,
                    InputType = inputType,
                    ReturnTypeNode = returnTypeNode,
                    ReturnType = returnType,
                    InvocationExpression = invocationExpression
                });
            }
        }

        private void SetSemanticModel(SyntaxNodeAnalysisContext context)
        {
            if (this.semanticModel == null)
            {
                this.semanticModel = context.SemanticModel;
            }
        }

        private bool IsActivityInvocation(InvocationExpressionSyntax invocationExpression)
        {
            if (invocationExpression != null && invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression)
            {
                var name = memberAccessExpression.Name;
                if (name != null
                    && (name.ToString().StartsWith("CallActivityAsync")
                        || name.ToString().StartsWith("CallActivityWithRetryAsync")))
                {
                    return true;
                }
            }
            
            return false;
        }

        private bool TryGetFunctionNameFromActivityInvocation(InvocationExpressionSyntax invocationExpression, out SyntaxNode functionNameNode, out string functionName)
        {
            var functionArgument = invocationExpression.ArgumentList.Arguments.FirstOrDefault();
            if (functionArgument != null)
            {
                functionNameNode = functionArgument.ChildNodes().FirstOrDefault();
                if (functionNameNode != null)
                {
                    SyntaxNodeUtils.TryParseFunctionName(semanticModel, functionNameNode, out functionName);
                    return functionName != null;
                }
            }

            functionNameNode = null;
            functionName = null;
            return false;
        }

        private bool TryGetInputNodeFromCallActivityInvocation(InvocationExpressionSyntax invocationExpression, out SyntaxNode inputNode)
        {
            var argumentList = invocationExpression.ArgumentList;
            if (argumentList != null)
            {
                var arguments = argumentList.Arguments;
                if (arguments != null && arguments.Count > 1)
                {
                    var lastArgumentNode = arguments.Last();

                    //An Argument node will always have a child node
                    inputNode = lastArgumentNode.ChildNodes().First();
                    return true;
                }
            }

            inputNode = null;
            return false;
        }

        public void FindActivityFunction(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            if (context.Node is AttributeSyntax attribute
                && SyntaxNodeUtils.IsActivityTriggerAttribute(attribute))
            {
                if (!SyntaxNodeUtils.TryGetFunctionName(semanticModel, attribute, out string functionName))
                {
                    //Do not store ActivityFunctionDefinition if there is no function name
                    return;
                }

                if (!SyntaxNodeUtils.TryGetMethodReturnTypeNode(attribute, out SyntaxNode returnTypeNode))
                {
                    //Do not store ActivityFunctionDefinition if there is no return type
                    return;
                }

                SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, returnTypeNode, out ITypeSymbol returnType);

                SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(context, attribute, out SyntaxNode parameterNode);

                TryGetDefinitionInputType(semanticModel, parameterNode, out ITypeSymbol inputType);

                availableFunctions.Add(new ActivityFunctionDefinition
                {
                    FunctionName = functionName,
                    ParameterNode = parameterNode,
                    InputType = inputType,
                    ReturnTypeNode = returnTypeNode,
                    ReturnType = returnType
                });
            }
        }

        private static bool TryGetDefinitionInputType(SemanticModel semanticModel, SyntaxNode parameterNode, out ITypeSymbol definitionInputType)
        {
            if (SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, parameterNode, out definitionInputType))
            {
                if (SyntaxNodeUtils.IsDurableActivityContext(definitionInputType))
                {
                    return TryGetInputTypeFromContext(semanticModel, parameterNode, out definitionInputType);
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
                    var identifierName = memberAccessExpression.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierName));
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
    }
}
