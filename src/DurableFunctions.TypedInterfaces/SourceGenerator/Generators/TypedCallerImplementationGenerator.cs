// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public abstract class TypedCallerImplementationGenerator : BaseGenerator
    {
        protected MethodDeclarationSyntax GenerateCallMethodWithRetry(DurableFunction function)
        {
            return GenerateCallerMethod(function, withRetry: true);
        }

        protected MethodDeclarationSyntax GenerateCallMethodWithoutRetry(DurableFunction function)
        {
            return GenerateCallerMethod(function, withRetry: false);
        }

        private MethodDeclarationSyntax GenerateCallerMethod(
            DurableFunction function,
            bool withRetry
        )
        {
            var parameters = function.Parameters;

            var methodName = $"{function.Name}{(withRetry ? "WithRetry" : string.Empty)}";

            var leadingTrivia = AsCrefSummary(function.FullTypeName);
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);

            var parameterList = AsParameterList()
                .AddParameters(withRetry ? AsParameter("RetryOptions", "options") : null)
                .AddParameters(function.Parameters.Select(p => AsParameter(p.Type.ToString(), p.Name)).ToArray());

            var callMethodName = (function.Kind == DurableFunctionKind.Orchestration) ?
                                $"CallSubOrchestrator{((withRetry) ? "WithRetry" : string.Empty)}Async" :
                                $"CallActivity{((withRetry) ? "WithRetry" : string.Empty)}Async";
            var callGenerics = function.CallGenerics;
            var functionNameParameter = $"\"{function.Name}\"";
            var callRetryParameter = withRetry ? ", options" : string.Empty;
            var callContextParameters = (parameters.Count == 0) ?
                                    ", null" :
                                    (parameters.Count == 1) ?
                                            $", {parameters[0].Name}" :
                                            $", ({string.Join(",", parameters.Select(p => p.Name))})";
            var callParameters = $"{functionNameParameter}{callRetryParameter}{callContextParameters}";


            var bodyText = $"return _context.{callMethodName}{callGenerics}({callParameters});";
            var returnStatement = SyntaxFactory.ParseStatement(bodyText);
            var bodyBlock = SyntaxFactory.Block(returnStatement);

            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(function.ReturnType), methodName)
                .WithModifiers(modifiers)
                .WithLeadingTrivia(leadingTrivia)
                .WithParameterList(parameterList)
                .WithBody(bodyBlock);
        }

        protected MethodDeclarationSyntax GenerateStartMethod(
            DurableFunction function
        )
        {
            const string instanceParameterName = "instance";

            var parameters = function.Parameters;

            var methodName = $"Start{function.Name}";

            var leadingTrivia = AsCrefSummary(function.FullTypeName);
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);

            var parameterList = AsParameterList()
                .AddParameters(function.Parameters.Select(p => AsParameter(p.Type.ToString(), p.Name)).ToArray())
                .AddParameters(AsParameter("string", instanceParameterName).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))));

            var callMethodName = "StartNewOrchestration";
            var functionNameParameter = $"\"{function.Name}\"";
            var callContextParameters = (parameters.Count == 0) ?
                                    ", null" :
                                    (parameters.Count == 1) ?
                                            $", {parameters[0].Name}" :
                                            $", ({string.Join(",", parameters.Select(p => p.Name))})";
            var callParameters = $"{functionNameParameter}{callContextParameters}, {instanceParameterName}";


            var bodyText = $"return _context.{callMethodName}({callParameters});";
            var returnStatement = SyntaxFactory.ParseStatement(bodyText);
            var bodyBlock = SyntaxFactory.Block(returnStatement);

            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("string"), methodName)
                .WithModifiers(modifiers)
                .WithLeadingTrivia(leadingTrivia)
                .WithParameterList(parameterList)
                .WithBody(bodyBlock);
        }
    }
}
