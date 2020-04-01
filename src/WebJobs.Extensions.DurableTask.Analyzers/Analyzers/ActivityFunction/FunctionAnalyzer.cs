﻿// Copyright (c) .NET Foundation. All rights reserved.
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
            NameAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
            ArgumentAnalyzer.ReportProblems(context, semanticModel, availableFunctions, calledFunctions);
            FunctionReturnTypeAnalyzer.ReportProblems(context, semanticModel, availableFunctions, calledFunctions);
        }

        public void FindActivityCall(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is InvocationExpressionSyntax invocationExpression
                && SyntaxNodeUtils.IsInsideFunction(context.SemanticModel, invocationExpression)
                && IsActivityInvocation(invocationExpression))
            {
                SetSemanticModel(context);

                if (!TryGetFunctionNameFromActivityInvocation(invocationExpression, out SyntaxNode functionNameNode))
                {
                    //Do not store ActivityFunctionCall if there is no function name
                    return;
                }

                SyntaxNodeUtils.TryGetTypeArgumentNode((MemberAccessExpressionSyntax)invocationExpression.Expression, out SyntaxNode returnTypeNode);

                TryGetInputNodeFromCallActivityInvocation(invocationExpression, out SyntaxNode inputNode);

                calledFunctions.Add(new ActivityFunctionCall
                {
                    Name = functionNameNode.ToString().GetCleanedFunctionName(),
                    NameNode = functionNameNode,
                    ParameterNode = inputNode,
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
                if (name.ToString().StartsWith("CallActivityAsync") || name.ToString().StartsWith("CallActivityWithRetryAsync"))
                {
                    return true;
                }
            }
            
            return false;
        }

        private bool TryGetFunctionNameFromActivityInvocation(InvocationExpressionSyntax invocationExpression, out SyntaxNode functionNameNode)
        {
            functionNameNode = invocationExpression.ArgumentList.Arguments.FirstOrDefault();
            return functionNameNode != null;
        }

        private bool TryGetInputNodeFromCallActivityInvocation(InvocationExpressionSyntax invocationExpression, out SyntaxNode inputNode)
        {
            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments != null && arguments.Count > 1)
            {
                var lastArgumentNode = arguments.Last();

                //An Argument node will always have a child node
                inputNode = lastArgumentNode.ChildNodes().First();
                return true;
            }

            inputNode = null;
            return false;
        }

        public void FindActivityFunction(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;
            if (SyntaxNodeUtils.IsActivityTriggerAttribute(attribute))
            {
                if (!SyntaxNodeUtils.TryGetFunctionNameAndNode(context.SemanticModel, attribute, out SyntaxNode attributeArgument, out string functionName))
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
