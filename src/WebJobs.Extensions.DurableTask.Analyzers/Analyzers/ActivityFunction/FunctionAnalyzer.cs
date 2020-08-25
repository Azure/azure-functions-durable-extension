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

                TryGetInputNodeFromCallActivityInvocation(invocationExpression, out SyntaxNode inputNode);

                calledFunctions.Add(new ActivityFunctionCall
                {
                    FunctionName = functionName,
                    NameNode = functionNameNode,
                    ArgumentNode = inputNode,
                    ReturnTypeNode = returnTypeNode,
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
            if (context.Node is AttributeSyntax attribute
                && SyntaxNodeUtils.IsActivityTriggerAttribute(attribute))
            {
                if (!SyntaxNodeUtils.TryGetFunctionName(context.SemanticModel, attribute, out string functionName))
                {
                    //Do not store ActivityFunctionDefinition if there is no function name
                    return;
                }

                if (!SyntaxNodeUtils.TryGetMethodReturnTypeNode(attribute, out SyntaxNode returnTypeNode))
                {
                    //Do not store ActivityFunctionDefinition if there is no return type
                    return;
                }

                SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(context, attribute, out SyntaxNode parameterNode);

                availableFunctions.Add(new ActivityFunctionDefinition
                {
                    FunctionName = functionName,
                    ParameterNode = parameterNode,
                    ReturnTypeNode = returnTypeNode
                });
            }
        }
    }
}
