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
    /// <summary>
    /// Collects ActivityFunctionDefinitions and ActivityFunctionCalls and diagnoses issues on them.
    /// Requires full solution analysis.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FunctionAnalyzer : DiagnosticAnalyzer
    {
        private List<ActivityFunctionDefinition> availableFunctions = new List<ActivityFunctionDefinition>();
        private List<ActivityFunctionCall> calledFunctions = new List<ActivityFunctionCall>();
        private SemanticModel semanticModel;
        private OrchestratorMethodCollector orchestratorMethodCollector;

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
            functionAnalyzer.orchestratorMethodCollector = new OrchestratorMethodCollector();

            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.orchestratorMethodCollector.FindOrchestratorMethods, SyntaxKind.MethodDeclaration);
                compilation.RegisterSyntaxNodeAction(functionAnalyzer.FindActivityFunctionDefinition, SyntaxKind.Attribute);

                compilation.RegisterCompilationEndAction(functionAnalyzer.CompilationEndActions);
            });
        }

        private void CompilationEndActions(CompilationAnalysisContext context)
        {
            this.FindActivityCalls();

            this.RegisterAnalyzers(context);
        }

        private void RegisterAnalyzers(CompilationAnalysisContext context)
        {
            NameAnalyzer.ReportProblems(context, this.semanticModel, this.availableFunctions, this.calledFunctions);
            ArgumentAnalyzer.ReportProblems(context, this.availableFunctions, this.calledFunctions);
            FunctionReturnTypeAnalyzer.ReportProblems(context, this.availableFunctions, this.calledFunctions);
        }

        private void FindActivityCalls()
        {
            var orchestratorMethods = this.orchestratorMethodCollector.GetOrchestratorMethods();

            foreach (MethodInformation methodInformation in orchestratorMethods)
            {
                var declaration = methodInformation.Declaration;
                if (declaration != null)
                {
                    var invocationExpressions = declaration.DescendantNodes().OfType<InvocationExpressionSyntax>();

                    this.semanticModel = methodInformation.SemanticModel;

                    foreach(var invocation in invocationExpressions)
                    {
                        if (IsActivityInvocation(invocation))
                        {
                            if (!TryGetFunctionNameFromActivityInvocation(invocation, out SyntaxNode functionNameNode, out string functionName))
                            {
                                //Do not store ActivityFunctionCall if there is no function name
                                return;
                            }

                            SyntaxNodeUtils.TryGetTypeArgumentIdentifier((MemberAccessExpressionSyntax)invocation.Expression, out SyntaxNode returnTypeNode);

                            SyntaxNodeUtils.TryGetITypeSymbol(this.semanticModel, returnTypeNode, out ITypeSymbol returnType);

                            TryGetInputNodeFromCallActivityInvocation(this.semanticModel, invocation, out SyntaxNode inputNode);

                            SyntaxNodeUtils.TryGetITypeSymbol(this.semanticModel, inputNode, out ITypeSymbol inputType);

                            this.calledFunctions.Add(new ActivityFunctionCall
                            {
                                FunctionName = functionName,
                                NameNode = functionNameNode,
                                InputNode = inputNode,
                                InputType = inputType,
                                ReturnTypeNode = returnTypeNode,
                                ReturnType = returnType,
                                InvocationExpression = invocation
                            });
                        }
                    }
                }
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
                    SyntaxNodeUtils.TryParseFunctionName(this.semanticModel, functionNameNode, out functionName);
                    return functionName != null;
                }
            }

            functionNameNode = null;
            functionName = null;
            return false;
        }

        private static bool TryGetInputNodeFromCallActivityInvocation(SemanticModel semanticModel, InvocationExpressionSyntax invocationExpression, out SyntaxNode inputNode)
        {
            // If method invocation is a custom CallActivity extension method defined in user code
            if (SyntaxNodeUtils.TryGetDeclaredSyntaxNode(semanticModel, invocationExpression, out SyntaxNode declaration))
            {
                if (TryGetSpecificParameterIndex(declaration, "object input", out int inputParameterIndex))
                {
                    if (TryGetInvocationArguments(invocationExpression, out IEnumerable<ArgumentSyntax> arguments))
                    {
                        var argumentNode = arguments.ElementAt(inputParameterIndex);
                        inputNode = argumentNode.ChildNodes().First();
                        return true;
                    }
                }
            }
            // else assume CallActivity is a DurableFunctions method
            else
            {
                if (TryGetInvocationArguments(invocationExpression, out IEnumerable<ArgumentSyntax> arguments))
                {
                    // Input node is currently the last argument on CallActivity* methods. If this is changed, this will not be sufficient to 
                    // determine which argument is meant to represent the input.
                    var argumentNode = arguments.Last();
                    inputNode = argumentNode.ChildNodes().First();
                    return true;
                }
            }

            inputNode = null;
            return false;
        }

        private static bool TryGetSpecificParameterIndex(SyntaxNode declaration, string parameterToFind, out int inputParameterIndex)
        {
            if (declaration is MethodDeclarationSyntax methodDeclaration)
            {
                var parameters = methodDeclaration.ParameterList.ChildNodes();
                var length = parameters.Count();
                for (int i = 0; i < length; i++)
                {
                    if (parameters.ElementAt(i).ToString() == parameterToFind)
                    {
                        inputParameterIndex = i;

                        if (IsExtensionMethod(parameters))
                        {
                            inputParameterIndex--;
                        }

                        return true;
                    }
                }
            }

            inputParameterIndex = int.MinValue;
            return false;
        }

        private static bool IsExtensionMethod(IEnumerable<SyntaxNode> parameters)
        {
            var firstParameter = parameters.ElementAt(0);
            if (firstParameter.ToString().StartsWith("this IDurableOrchestrationContext"))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetInvocationArguments(InvocationExpressionSyntax invocationExpression, out IEnumerable<ArgumentSyntax> arguments)
        {
            var argumentList = invocationExpression.ArgumentList;
            if (argumentList != null)
            {
                arguments = argumentList.Arguments;
                if (arguments != null && arguments.Any())
                {
                    return true;
                }
            }

            arguments = null;
            return false;
        }

        public void FindActivityFunctionDefinition(SyntaxNodeAnalysisContext context)
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

                SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(attribute, out SyntaxNode parameterNode);

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

