// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public abstract class BaseGenerator
    {
        private const string Namespace = "Microsoft.Azure.WebJobs.Extensions.DurableTask.TypedInterfaces";

        protected static NamespaceDeclarationSyntax GenerateNamespace()
        {
            return SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(Namespace));
        }

        protected SyntaxTriviaList AsCrefSummary(string fullTypeName)
        {
            return SyntaxFactory.ParseLeadingTrivia(
                "/// <summary>\n" +
                $"/// See <see cref=\"{fullTypeName}\"/>\n" +
                "/// </summary>\n"
            );
        }

        protected ExpressionStatementSyntax AsSimpleAssignmentExpression(string leftIdentifier, string rightIdentifier)
        {
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(leftIdentifier),
                    SyntaxFactory.IdentifierName(rightIdentifier)
                )
            );
        }

        protected ParameterListSyntax AsParameterList(params ParameterSyntax[] parameters)
        {
            return SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(
                    parameters
                )
            );
        }

        protected BlockSyntax AsBlock(params StatementSyntax[] statements)
        {
            return SyntaxFactory.Block(
                statements
            );
        }

        protected ParameterSyntax AsParameter(string typeName, string parameterName)
        {
            return SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)).WithType(SyntaxFactory.ParseTypeName(typeName));
        }

        protected FieldDeclarationSyntax AsField(string typeName, string fieldName)
        {
            return SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName(typeName)
                        )
                        .AddVariables(SyntaxFactory.VariableDeclarator(fieldName)
                        )
                )
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        }

        protected UsingDirectiveSyntax[] AsUsings(IEnumerable<string> namespaces)
        {
            return namespaces.Select(AsUsing).ToArray();
        }

        protected SyntaxTokenList AsModifierList(params SyntaxKind[] syntaxKinds)
        {
            return SyntaxFactory.TokenList(syntaxKinds.Select(sk => SyntaxFactory.Token(sk)));
        }

        protected BaseListSyntax AsBaseList(params string[] names)
        {
            return SyntaxFactory.BaseList(
                SyntaxFactory.SeparatedList<BaseTypeSyntax>(
                    names.Select(n =>
                        SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.IdentifierName(n)
                        )
                    )
                )
            );
        }

        protected UsingDirectiveSyntax AsUsing(string @namespace)
        {
            return SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(@namespace));
        }

        protected PropertyDeclarationSyntax AsPublicPropertyWithGetter(string typeName, string propertyName)
        {
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);

            return AsPropertyWithGetter(typeName, propertyName).WithModifiers(modifiers);
        }

        protected PropertyDeclarationSyntax AsPropertyWithGetter(string typeName, string propertyName)
        {
            return SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.IdentifierName(typeName),
                SyntaxFactory.Identifier(propertyName)
            ).WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.List(
                        new[]
                        {
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                        }
                    )
                )
            );
        }

    }
}