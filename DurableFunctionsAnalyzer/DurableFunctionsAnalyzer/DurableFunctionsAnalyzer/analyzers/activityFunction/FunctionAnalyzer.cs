// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WebJobs.Extensions.DurableTask.Analyzers
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
                if (SyntaxNodeUtils.TryGetFunctionAttribute(attributeExpression, out SyntaxNode functionAttribute))
                {
                    if (SyntaxNodeUtils.TryGetFunctionName(functionAttribute, out string functionName))
                    {
                        if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(attributeExpression, context, out SyntaxNode inputTypeNode))
                        {
                            ITypeSymbol inputType = context.SemanticModel.GetTypeInfo(inputTypeNode).Type;
                            if (inputType.ToString().Equals("Microsoft.Azure.WebJobs.IDurableActivityContext") || inputType.ToString().Equals("Microsoft.Azure.WebJobs.DurableActivityContext"))
                            {
                                if (!TryGetInputTypeFromDurableContextCall(out inputType, context, attributeExpression))
                                {
                                    return;
                                }
                            }

                            if (SyntaxNodeUtils.TryGetReturnType(attributeExpression, context, out ITypeSymbol returnType))
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
