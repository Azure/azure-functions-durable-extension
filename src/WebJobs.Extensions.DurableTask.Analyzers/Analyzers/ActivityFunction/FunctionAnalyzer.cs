﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FunctionAnalyzer : DiagnosticAnalyzer
    {
        public List<ActivityFunctionDefinition> availableFunctions = new List<ActivityFunctionDefinition>();
        public List<ActivityFunctionCall> calledFunctions = new List<ActivityFunctionCall>();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    NameAnalyzer.MissingRule,
                    NameAnalyzer.CloseRule,
                    ArgumentAnalyzer.Rule,
                    FunctionReturnTypeAnalyzer.Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            FunctionAnalyzer functionAnalyzer = new FunctionAnalyzer();
            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.FindActivityCalls, SyntaxKind.InvocationExpression);
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.FindActivities, SyntaxKind.Attribute);

                compilation.RegisterCompilationEndAction(functionAnalyzer.RegisterAnalyzers);
            });
        }


        private void RegisterAnalyzers(CompilationAnalysisContext context)
        {
            ArgumentAnalyzer argumentAnalyzer = new ArgumentAnalyzer();
            NameAnalyzer nameAnalyzer = new NameAnalyzer();
            FunctionReturnTypeAnalyzer returnTypeAnalyzer = new FunctionReturnTypeAnalyzer();

            argumentAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
            nameAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
            returnTypeAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
        }

        public void FindActivityCalls(SyntaxNodeAnalysisContext context)
        {
            if (TryGetCallActivityInvocation(context, out InvocationExpressionSyntax invocationExpression))
            {
                if (!TryGetFunctionNameFromCallActivityInvocation(invocationExpression, out SyntaxNode functionNameNode))
                {
                    //Do not store ActivityFunctionCall if there is no function name
                    return;
                }

                var returnTypeName = GetReturnTypeNameFromCallActivityInvocation(context, invocationExpression);

                var inputNode = GetInputNodeFromCallActivityInvocation(invocationExpression);
                var inputType = context.SemanticModel.GetTypeInfo(inputNode).Type;
                var inputTypeName = GetQualifiedTypeName(inputType);

                calledFunctions.Add(new ActivityFunctionCall
                {
                    Name = functionNameNode.ToString().Trim('"'),
                    NameNode = functionNameNode,
                    ParameterNode = inputNode,
                    ParameterType = inputTypeName,
                    ExpectedReturnType = returnTypeName,
                    InvocationExpression = invocationExpression
                });
            }
        }

        private bool TryGetCallActivityInvocation(SyntaxNodeAnalysisContext context, out InvocationExpressionSyntax invocationExpression)
        {
            invocationExpression = context.Node as InvocationExpressionSyntax;
            if (invocationExpression != null)
            {
                var expression = invocationExpression.Expression as MemberAccessExpressionSyntax;
                if (expression != null)
                {
                    var name = expression.Name;
                    if (name.ToString().StartsWith("CallActivityAsync") || name.ToString().StartsWith("CallActivityWithRetryAsync"))
                    {
                        return true;
                    }
                }
            }

            invocationExpression = null;
            return false;
        }

        private bool TryGetFunctionNameFromCallActivityInvocation(InvocationExpressionSyntax invocationExpression, out SyntaxNode functionNameNode)
        {
            functionNameNode = invocationExpression.ArgumentList.Arguments.FirstOrDefault();
            if (functionNameNode != null)
            {
                return true;
            }

            return false;
        }

        private string GetReturnTypeNameFromCallActivityInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpression)
        {
            if (SyntaxNodeUtils.TryGetTypeArgumentList((MemberAccessExpressionSyntax)invocationExpression.Expression, out SyntaxNode identifierNode))
            {
                var returnType = context.SemanticModel.GetTypeInfo(identifierNode).Type;
                var returnTypeName = GetQualifiedTypeName(returnType);
                if (returnTypeName != null)
                {
                    return "System.Threading.Tasks.Task<" + returnTypeName + ">";
                }
            }

            return "System.Threading.Tasks.Task";
        }

        private SyntaxNode GetInputNodeFromCallActivityInvocation(InvocationExpressionSyntax invocationExpression)
        {
            var argumentNode = invocationExpression.ArgumentList.Arguments.LastOrDefault();
            if (argumentNode != null)
            {
                var inputNode = argumentNode.ChildNodes().First();
                return inputNode;
            }
            
            return null;
        }

        private string GetQualifiedTypeName(ITypeSymbol typeInfo)
        {
            if (typeInfo != null)
            {
                var genericType = "";
                if (typeInfo is INamedTypeSymbol)
                {
                    var namedTypeSymbol = typeInfo as INamedTypeSymbol;
                    var tupleUnderlyingType = namedTypeSymbol.TupleUnderlyingType;
                    if (tupleUnderlyingType != null)
                    {
                        return $"Tuple<{string.Join(", ", tupleUnderlyingType.TypeArguments.Select(x => GetQualifiedTypeName(x)))}>";
                    }
                    else if (namedTypeSymbol.TypeArguments.Any())
                    {
                        genericType = "<" + GetQualifiedTypeName(namedTypeSymbol.TypeArguments.First()) + ">";
                    }
                }

                var qualifiedTypeName = typeInfo.ContainingNamespace?.ToString() + "." + typeInfo.Name?.ToString() + genericType;
                return qualifiedTypeName;
            }

            return "";
        }

        public void FindActivities(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;
            if (SyntaxNodeUtils.IsActivityTriggerAttribute(attribute))
            {
                if (!TryGetFunctionName(attribute, out string functionName))
                {
                    //Do not store ActivityFunctionDefinition if there is no function name
                    return;
                }

                if (!TryGetReturnType(context, attribute, out ITypeSymbol returnType))
                {
                    //Do not store ActivityFunctionDefinition if there is no return type
                    return;
                }

                var returnTypeName = GetQualifiedTypeName(returnType);
                var inputTypeName = GetActivityFunctionInputTypeName(context, attribute);

                availableFunctions.Add(new ActivityFunctionDefinition
                {
                    FunctionName = functionName,
                    InputType = inputTypeName,
                    ReturnType = returnTypeName
                });
            }
        }

        private bool TryGetFunctionName(AttributeSyntax attributeExpression, out string functionName)
        {
            if (SyntaxNodeUtils.TryGetFunctionNameParameterNode(attributeExpression, out SyntaxNode attributeArgument))
            {
                functionName = attributeArgument.ToString().Trim('"');
                return true;
            }

            functionName = null;
            return false;
        }

        private static bool TryGetReturnType(SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression, out ITypeSymbol returnType)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(attributeExpression, out SyntaxNode methodDeclaration))
            {
                returnType = context.SemanticModel.GetTypeInfo((methodDeclaration as MethodDeclarationSyntax).ReturnType).Type;
                return true;
            }

            returnType = null;
            return false;
        }

        private string GetActivityFunctionInputTypeName(SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression)
        {
            if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(context, attributeExpression, out SyntaxNode inputTypeNode))
            {
                var inputType = context.SemanticModel.GetTypeInfo(inputTypeNode).Type;
                if (inputType.ToString().Equals("Microsoft.Azure.WebJobs.IDurableActivityContext") || inputType.ToString().Equals("Microsoft.Azure.WebJobs.DurableActivityContext"))
                {
                    TryGetInputTypeFromDurableContextCall(context, attributeExpression, out inputType);
                }

                return GetQualifiedTypeName(inputType);
            }

            return null;
        }

        private static bool TryGetInputTypeFromDurableContextCall(SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression, out ITypeSymbol inputTypeNode)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(attributeExpression, out SyntaxNode methodDeclaration))
            {
                var memberAccessExpressionList = methodDeclaration.DescendantNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                foreach (var memberAccessExpression in memberAccessExpressionList)
                {
                    var identifierName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                    if (identifierName.Any())
                    {
                        var identifierNameType = context.SemanticModel.GetTypeInfo(identifierName.First()).Type.Name;
                        if (identifierNameType.Equals("IDurableActivityContext") || identifierNameType.Equals("DurableActivityContext"))
                        {
                            var genericName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName));
                            if (genericName.Any())
                            {
                                var typeArgumentListEnumerable = genericName.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList));
                                if (typeArgumentListEnumerable.Any())
                                {
                                    inputTypeNode = context.SemanticModel.GetTypeInfo(typeArgumentListEnumerable.First().ChildNodes().First()).Type;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            inputTypeNode = null;
            return false;
        }
    }
}
