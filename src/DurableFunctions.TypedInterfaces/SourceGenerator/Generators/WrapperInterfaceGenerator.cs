// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public abstract class WrapperInterfaceGenerator : BaseGenerator
    {
        protected abstract INamedTypeSymbol NamedTypeSymbol { get; }
        protected abstract string InterfaceName { get; }

        protected abstract SyntaxList<UsingDirectiveSyntax> GetAdditionalUsings();
        protected abstract List<PropertyDeclarationSyntax> GetAdditionalProperties();

        public CompilationUnitSyntax Generate()
        {
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);
            var baseList = AsBaseList(NamedTypeSymbol);
            var properties = GetAdditionalProperties();

            var @interface = SyntaxFactory.InterfaceDeclaration(InterfaceName)
                .WithModifiers(modifiers)
                .WithBaseList(baseList)
                .WithMembers(
                    SyntaxFactory.List<MemberDeclarationSyntax>(properties)
                );

            var @namespace = GenerateNamespace()
                .AddMembers(@interface);

            var usings = GetAdditionalUsings();

            return SyntaxFactory.CompilationUnit().WithUsings(usings).AddMembers(@namespace).NormalizeWhitespace();
        }

        protected BaseListSyntax AsBaseList(params INamedTypeSymbol[] symbols)
        {
            return SyntaxFactory.BaseList(
                SyntaxFactory.SeparatedList<BaseTypeSyntax>(
                    symbols.Select(s =>
                        SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.IdentifierName(s.Name)
                        )
                    )
                )
            );
        }
    }
}
