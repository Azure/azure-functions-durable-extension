// Copyright (c) .NET Foundation. All rights reserved.
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
                    return;
                }

                if (!TryGetInputNodeFromCallActivityInvocation(invocationExpression, out SyntaxNode inputNode))
                {
                    return;
                }

                var inputType = context.SemanticModel.GetTypeInfo(inputNode).Type;

                string returnTypeName;
                if (!TryGetReturnTypeFromCallActivityInvocation(context, invocationExpression, out ITypeSymbol returnType))
                {
                    returnTypeName = "System.Threading.Tasks.Task";
                }
                else
                {
                    returnTypeName = GetQualifiedTypeName(returnType);
                    returnTypeName = "System.Threading.Tasks.Task<" + returnTypeName + ">";
                }

                calledFunctions.Add(new ActivityFunctionCall
                {
                    Name = functionNameNode.ToString().Trim('"'),
                    NameNode = functionNameNode,
                    ParameterNode = inputNode,
                    ParameterType = GetQualifiedTypeName(inputType),
                    ExpectedReturnType = returnTypeName,
                    InvocationExpression = invocationExpression
                });
            }
        }

        private bool TryGetReturnTypeFromCallActivityInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationExpression, out ITypeSymbol returnType)
        {
            if (SyntaxNodeUtils.TryGetTypeArgumentList((MemberAccessExpressionSyntax)invocationExpression.Expression, out SyntaxNode identifierNode))
            {
                returnType = context.SemanticModel.GetTypeInfo(identifierNode).Type;
                return true;
            }

            returnType = null;
            return false;
        }

        private bool TryGetInputNodeFromCallActivityInvocation(InvocationExpressionSyntax invocationExpression, out SyntaxNode inputNode)
        {
            inputNode = invocationExpression.ArgumentList.Arguments.LastOrDefault();
            if (inputNode != null)
            {
                return true;
            }

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

        private string GetQualifiedTypeName(ITypeSymbol typeInfo)
        {
            if (typeInfo == null)
            {
                return "";
            }
            var tupleunderlyingtype = (typeInfo as INamedTypeSymbol).TupleUnderlyingType;
            if (tupleunderlyingtype != null)
            {
                return $"Tuple<{string.Join(", ", tupleunderlyingtype.TypeArguments.Select(x => GetQualifiedTypeName(x)))}>";
            }

            var namedSymbol = typeInfo as INamedTypeSymbol;
            var genericType = "";
            if (namedSymbol.TypeArguments.Any())
            {
                genericType = "<" + GetQualifiedTypeName(namedSymbol.TypeArguments.First()) + ">";
            }
            var typeName = "";
            if (typeInfo.OriginalDefinition.ContainingNamespace.ToString() != "<global namespace>")
                typeName = typeInfo.OriginalDefinition.ContainingNamespace + "." + typeInfo.OriginalDefinition?.Name;
            else
                typeName = "System." + typeInfo.OriginalDefinition?.Name;
            var returnType = typeName + genericType;
            if (returnType == "System.Int")
                return returnType + "32";
            return returnType;
        }

        private bool TryGetQualifiedTypeName(ITypeSymbol typeInfo, out string qualifiedTypeName)
        {
            if (typeInfo != null)
            {
                qualifiedTypeName = typeInfo.ContainingNamespace.ToString() + "." + typeInfo.Name.ToString();
                return true;
            }

            qualifiedTypeName = null;
            return false;
        }

        public void FindActivities(SyntaxNodeAnalysisContext context)
        {
            if (SyntaxNodeUtils.TryGetActivityTriggerAttributeExpression(context, out AttributeSyntax attributeExpression))
            {
                if (!TryGetFunctionName(attributeExpression, out string functionName))
                {
                    return;
                }

                if (!TryGetActivityFunctionInputType(context, attributeExpression, out ITypeSymbol inputType))
                {
                    return;
                }

                if (!SyntaxNodeUtils.TryGetReturnType(context, attributeExpression, out ITypeSymbol returnType))
                {
                    return;
                }

                availableFunctions.Add(new ActivityFunctionDefinition
                {
                    FunctionName = functionName,
                    InputType = GetQualifiedTypeName(inputType),
                    ReturnType = GetQualifiedTypeName(returnType)
                });
            }
        }

        private bool TryGetActivityFunctionInputType(SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression, out ITypeSymbol inputType)
        {
            if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(context, attributeExpression, out SyntaxNode inputTypeNode))
            {
                inputType = context.SemanticModel.GetTypeInfo(inputTypeNode).Type;
                if (inputType.ToString().Equals("Microsoft.Azure.WebJobs.IDurableActivityContext") || inputType.ToString().Equals("Microsoft.Azure.WebJobs.DurableActivityContext"))
                {
                    if (!TryGetInputTypeFromDurableContextCall(out inputType, context, attributeExpression))
                    {
                        return true;
                    }
                }
            }

            inputType = null;
            return false;
        }

        private bool TryGetFunctionName(AttributeSyntax attributeExpression, out string functionName)
        {
            if (SyntaxNodeUtils.TryGetFunctionNameNode(attributeExpression, out SyntaxNode attributeArgument))
            {
                functionName = attributeArgument.ToString().Trim('"');
                return true;
            }

            functionName = null;
            return false;
        }

        private static bool TryGetInputTypeFromDurableContextCall(out ITypeSymbol inputTypeNode, SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression)
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
