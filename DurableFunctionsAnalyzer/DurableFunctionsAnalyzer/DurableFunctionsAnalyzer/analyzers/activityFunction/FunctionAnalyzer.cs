// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableFunctionsAnalyzer.analyzers.function;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DurableFunctionsAnalyzer.analyzers.activityFunction
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    class FunctionAnalyzer : DiagnosticAnalyzer
    {
        List<ActivityFunctionDefinition> availableFunctions = new List<ActivityFunctionDefinition>();
        List<ActivityFunctionCall> calledFunctions = new List<ActivityFunctionCall>();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    NameAnalyzer.MissingRule,
                    NameAnalyzer.CloseRule,
                    ArgumentAnalyzer.Rule,
                    ReturnTypeAnalyzer.Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
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
            ReturnTypeAnalyzer returnTypeAnalyzer = new ReturnTypeAnalyzer();

            argumentAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
            nameAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
            returnTypeAnalyzer.ReportProblems(context, availableFunctions, calledFunctions);
        }

        public void FindActivityCalls(SyntaxNodeAnalysisContext context)
        {
            var invocationExpression = context.Node as InvocationExpressionSyntax;
            if (invocationExpression != null)
            {

                var expression = invocationExpression.Expression as MemberAccessExpressionSyntax;
                if (expression != null)
                {
                    var name = expression.Name;
                    if (name.ToString().StartsWith("CallActivityAsync") || name.ToString().StartsWith("CallActivityWithRetryAsync"))
                    {
                        var functionName = invocationExpression.ArgumentList.Arguments.FirstOrDefault();
                        var argumentType = invocationExpression.ArgumentList.Arguments.Last();
                        var returnType = invocationExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                            .FirstOrDefault()?
                            .ChildNodes()
                            .Where(x => x.IsKind(SyntaxKind.GenericName))
                            .FirstOrDefault()?
                            .ChildNodes()
                            .Where(x => x.IsKind(SyntaxKind.TypeArgumentList))?
                            .FirstOrDefault();
                        var returnTypeName = "System.Threading.Tasks.Task";
                        if (returnType != null)
                        {
                            returnTypeName = GetQualifiedTypeName(context.SemanticModel.GetTypeInfo(returnType.ChildNodes().FirstOrDefault()).Type);
                            returnTypeName = "System.Threading.Tasks.Task<" + returnTypeName + ">";

                        }
                        var typeInfo = context.SemanticModel.GetTypeInfo(argumentType.ChildNodes().First());
                        var typeName = "";
                        if (typeInfo.Type == null)
                            return;
                        typeName = GetQualifiedTypeName(typeInfo.Type);
                        if (functionName != null && functionName.ToString().StartsWith("\""))
                            calledFunctions.Add(new ActivityFunctionCall
                            {
                                Name = functionName.ToString().Trim('"'),
                                NameNode = functionName,
                                ParameterNode = argumentType,
                                ParameterType = typeName,
                                ExpectedReturnType = returnTypeName,
                                ExpectedReturnTypeNode = invocationExpression
                            });
                    }
                }
            }
        }

        private string GetQualifiedTypeName(ITypeSymbol typeInfo)
        {
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

        public void FindActivities(SyntaxNodeAnalysisContext context)
        {
            var attributeExpression = context.Node as AttributeSyntax;
            if (attributeExpression != null && attributeExpression.ChildNodes().First().ToString() == "ActivityTrigger")
            {
                if (SyntaxNodeUtils.TryGetFunctionAttribute(out SyntaxNode functionAttribute, attributeExpression))
                {
                    if (SyntaxNodeUtils.TryGetFunctionName(out string functionName, functionAttribute))
                    {
                        if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(out SyntaxNode inputTypeNode, attributeExpression, context))
                        {
                            ITypeSymbol inputType = context.SemanticModel.GetTypeInfo(inputTypeNode).Type;
                            if (inputType.ToString().Equals("Microsoft.Azure.WebJobs.IDurableActivityContext"))
                            {
                                if (TryGetInputTypeFromDurableContextCall(out inputType, context, attributeExpression))
                                {
                                    if (SyntaxNodeUtils.TryGetReturnType(out ITypeSymbol returnType, attributeExpression, context))
                                    {
                                        availableFunctions.Add(new ActivityFunctionDefinition
                                        {
                                            FunctionName = functionName,
                                            InputType = GetQualifiedTypeName(inputType),
                                            ReturnType = GetQualifiedTypeName(returnType)
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private SyntaxNode getInputTypeNodeFromContextCall(SyntaxNodeAnalysisContext context, SyntaxNode methodDeclaration)
        {
            var memberAccessExpressionList = methodDeclaration.DescendantNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression));
            foreach(var memberAccessExpression in memberAccessExpressionList)
            {
                var identifierName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                if (identifierName.Any())
                {
                    var identifierNameType = context.SemanticModel.GetTypeInfo(identifierName.First()).Type.Name;
                    if (identifierNameType.Equals("IDurableActivityContext"))
                    {
                        var genericName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName));
                        if (genericName.Any())
                        {
                            var typeArgumentList = genericName.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList));
                            if (typeArgumentList.Any())
                            {
                                var inputTypeNode = typeArgumentList.First().ChildNodes();
                                return inputTypeNode.First();
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static bool TryGetInputTypeFromDurableContextCall(out ITypeSymbol inputTypeNode, SyntaxNodeAnalysisContext context, AttributeSyntax attributeExpression)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(out SyntaxNode methodDeclaration, attributeExpression))
            {
                var memberAccessExpressionList = methodDeclaration.DescendantNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                foreach (var memberAccessExpression in memberAccessExpressionList)
                {
                    var identifierName = memberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                    if (identifierName.Any())
                    {
                        var identifierNameType = context.SemanticModel.GetTypeInfo(identifierName.First()).Type.Name;
                        if (identifierNameType.Equals("IDurableActivityContext"))
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
