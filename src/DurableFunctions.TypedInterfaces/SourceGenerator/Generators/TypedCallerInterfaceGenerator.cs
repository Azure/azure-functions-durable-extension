// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public abstract class TypedCallerInterfaceGenerator : BaseGenerator
    {
        protected readonly List<DurableFunction> functions;

        protected TypedCallerInterfaceGenerator(List<DurableFunction> functions)
        {
            this.functions = functions;
        }

        protected MethodDeclarationSyntax GenerateCallMethodWithRetry(DurableFunction function)
        {
            return GenerateCallerMethod(function, true);
        }

        protected MethodDeclarationSyntax GenerateCallMethodWithoutRetry(DurableFunction function)
        {
            return GenerateCallerMethod(function, false);
        }

        private MethodDeclarationSyntax GenerateCallerMethod(
            DurableFunction function,
            bool withRetry
        )
        {
            var parameters = function.Parameters;

            var methodName = $"{function.Name}{(withRetry ? "WithRetry" : string.Empty)}";

            var leadingTrivia = AsCrefSummary(function.FullTypeName);

            var parameterList = AsParameterList()
                .AddParameters(withRetry ? AsParameter("RetryOptions", "options") : null)
                .AddParameters(function.Parameters.Select(p => AsParameter(p.Type.ToString(), p.Name)).ToArray());

            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(function.ReturnType), methodName)
                .WithLeadingTrivia(leadingTrivia)
                .WithParameterList(parameterList)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        protected MethodDeclarationSyntax GenerateStartMethod(
            DurableFunction function
        )
        {
            const string instanceParameterName = "instance";

            var methodName = $"Start{function.Name}";

            var leadingTrivia = AsCrefSummary(function.FullTypeName);

            var parameterList = AsParameterList()
                .WithParameters(AsParameters(function.Parameters))
                .AddParameters(AsParameter("string", instanceParameterName).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))));

            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("string"), methodName)
                .WithLeadingTrivia(leadingTrivia)
                .WithParameterList(parameterList)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        protected SeparatedSyntaxList<ParameterSyntax> AsParameters(List<TypedParameter> parameters)
        {
            return SyntaxFactory.SeparatedList(
                parameters.Select(p => AsParameter(p.Type.ToString(), p.Name))
            );
        }
    }
}
