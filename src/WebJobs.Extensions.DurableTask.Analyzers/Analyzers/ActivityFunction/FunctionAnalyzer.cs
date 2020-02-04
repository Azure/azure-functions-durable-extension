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
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.FindActivityCall, SyntaxKind.InvocationExpression);
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.FindActivity, SyntaxKind.Attribute);

                compilation.RegisterCompilationEndAction(functionAnalyzer.RegisterAnalyzers);
            });
        }

        private void RegisterAnalyzers(CompilationAnalysisContext context)
        {
            ArgumentAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
            NameAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
            FunctionReturnTypeAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
        }

        public void FindActivityCall(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is InvocationExpressionSyntax invocationExpression && 
                SyntaxNodeUtils.IsInsideFunction(invocationExpression) && 
                IsCallActivityInvocation(invocationExpression))
            {
                if (!TryGetFunctionNameFromCallActivityInvocation(invocationExpression, out SyntaxNode functionNameNode))
                {
                    //Do not store ActivityFunctionCall if there is no function name
                    return;
                }

                var returnTypeName = GetReturnTypeNameFromCallActivityInvocation(context, invocationExpression);

                string inputTypeName = null;
                if (TryGetInputNodeFromCallActivityInvocation(invocationExpression, out SyntaxNode inputNode))
                {
                    var inputType = context.SemanticModel.GetTypeInfo(inputNode).Type;
                    inputTypeName = GetQualifiedTypeName(inputType);
                }

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

        private bool IsCallActivityInvocation(InvocationExpressionSyntax invocationExpression)
        {
            if (invocationExpression != null)
            {
                if (invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression)
                {
                    var name = memberAccessExpression.Name;
                    if (name.ToString().StartsWith("CallActivityAsync") || name.ToString().StartsWith("CallActivityWithRetryAsync"))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private bool TryGetFunctionNameFromCallActivityInvocation(InvocationExpressionSyntax invocationExpression, out SyntaxNode functionNameNode)
        {
            functionNameNode = invocationExpression.ArgumentList.Arguments.FirstOrDefault();
            return functionNameNode != null;
        }

        private string GetReturnTypeNameFromCallActivityInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpression)
        {
            if (SyntaxNodeUtils.TryGetTypeArgumentIdentifierNode((MemberAccessExpressionSyntax)invocationExpression.Expression, out SyntaxNode identifierNode))
            {
                var returnType = context.SemanticModel.GetTypeInfo(identifierNode).Type;
                return "System.Threading.Tasks.Task<" + GetQualifiedTypeName(returnType) + ">";
            }

            return "System.Threading.Tasks.Task";
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

        private string GetQualifiedTypeName(ITypeSymbol typeInfo)
        {
            if (typeInfo != null)
            {
                if (typeInfo is INamedTypeSymbol)
                {
                    var tupleUnderlyingType = ((INamedTypeSymbol)typeInfo).TupleUnderlyingType;
                    if (tupleUnderlyingType != null)
                    {
                        return $"System.Tuple<{string.Join(", ", tupleUnderlyingType.TypeArguments.Select(x => x.ToString()))}>";
                    }

                    return typeInfo.ToString();
                }

                if (!string.IsNullOrEmpty(typeInfo.Name))
                {
                    return typeInfo.ContainingNamespace?.ToString() + "." + typeInfo.Name.ToString();
                }
            }

            return "Unknown Type";
        }

        public void FindActivity(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;
            if (SyntaxNodeUtils.IsActivityTriggerAttribute(attribute))
            {
                if (!SyntaxNodeUtils.TryGetFunctionNameAndNode(attribute, out SyntaxNode attributeArgument, out string functionName))
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
                var inputType = GetActivityFunctionInputTypeName(context, attribute);
                var inputTypeName = GetQualifiedTypeName(inputType);

                availableFunctions.Add(new ActivityFunctionDefinition
                {
                    FunctionName = functionName,
                    InputType = inputTypeName,
                    ReturnType = returnTypeName
                });
            }
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

        private ITypeSymbol GetActivityFunctionInputTypeName(SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression)
        {
            if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(context, attributeExpression, out SyntaxNode inputTypeNode))
            {
                var inputType = context.SemanticModel.GetTypeInfo(inputTypeNode).Type;
                if (inputType.ToString().Equals("Microsoft.Azure.WebJobs.Extensions.DurableTask.IDurableActivityContext") 
                    || inputType.ToString().Equals("Microsoft.Azure.WebJobs.DurableActivityContext") 
                    || inputType.ToString().Equals("Microsoft.Azure.WebJobs.DurableActivityContextBase"))
                {
                    TryGetInputTypeFromDurableContextCall(context, attributeExpression, out inputType);
                }

                return inputType;
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
                                var typeArgumentList = genericName.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList)).FirstOrDefault();
                                if (typeArgumentList != null)
                                {
                                    inputTypeNode = context.SemanticModel.GetTypeInfo(typeArgumentList.ChildNodes().First()).Type;
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
