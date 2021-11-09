// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public abstract class WrapperImplementationGenerator : BaseGenerator
    {
        protected abstract INamedTypeSymbol NamedTypeSymbol { get; }
        protected abstract string InterfaceName { get; }
        protected abstract string ClassName { get; }
        protected abstract string ContextFieldName { get; }

        protected abstract SyntaxList<UsingDirectiveSyntax> GetAdditionalUsings();
        protected abstract ConstructorDeclarationSyntax GetConstructor();
        protected abstract PropertyDeclarationSyntax[] GetAdditionalProperties();

        private IEnumerable<IPropertySymbol> GetPropertySymbols()
        {
            var allInterfaces = NamedTypeSymbol.GetAllInterfacesIncludingThis();
            var allMembers = allInterfaces.SelectMany(nts => nts.GetMembers()).ToList();

            var propertySymbols = allMembers.OfType<IPropertySymbol>().ToList();
            var propertyIdSet = new HashSet<string>();

            // Dedupe overriden properties
            for (int i = 0; i < propertySymbols.Count; i++)
            {
                var propertySymbol = propertySymbols[i];
                var propertyid = $"{propertySymbol.Type.ToPrettyString()}_{propertySymbol.Name}";

                if (propertyIdSet.Contains(propertyid))
                {
                    propertySymbols.RemoveAt(i--);
                    continue;
                }

                propertyIdSet.Add(propertyid);
            }

            return propertySymbols;
        }

        private IEnumerable<IMethodSymbol> GetMethodSymbols()
        {
            var allInterfaces = NamedTypeSymbol.GetAllInterfacesIncludingThis();
            var allMembers = allInterfaces.SelectMany(nts => nts.GetMembers());
            return allMembers.OfType<IMethodSymbol>()
                .Where(m => m.MethodKind != MethodKind.PropertyGet && m.MethodKind != MethodKind.PropertySet);
        }

        public CompilationUnitSyntax Generate()
        {
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);
            var baseList = base.AsBaseList(InterfaceName);

            var members = new List<MemberDeclarationSyntax>();

            members.Add(GetContextField());
            members.AddRange(GetAdditionalProperties());
            members.AddRange(GetPropertySymbols().Select(PropertyDeclaration));
            members.Add(GetConstructor());
            members.AddRange(GetMethodSymbols().Select(MethodDeclaration));

            var @class = SyntaxFactory.ClassDeclaration(ClassName)
                .WithBaseList(baseList)
                .WithModifiers(modifiers)
                .WithMembers(SyntaxFactory.List(members));

            var @namespace = GenerateNamespace()
                .AddMembers(@class);

            var usings = GetAdditionalUsings();

            return SyntaxFactory.CompilationUnit().WithUsings(usings).AddMembers(@namespace).NormalizeWhitespace();
        }

        private FieldDeclarationSyntax GetContextField()
        {
            var modifier = AsModifierList(SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword);

            return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName(NamedTypeSymbol.Name)
                )
                .AddVariables(SyntaxFactory.VariableDeclarator(ContextFieldName))
            )
            .WithModifiers(modifier)
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        }

        private MethodDeclarationSyntax MethodDeclaration(IMethodSymbol methodSymbol)
        {
            var methodName = methodSymbol.Name;
            var returnTypeName = methodSymbol.ReturnType.ToPrettyString();

            var leadingTrivia = InheritDoc();
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);
            var typeParameters = AsTypeParameterList(methodSymbol.TypeParameters);
            var parameters = AsParameterList(methodSymbol.Parameters);
            var constraints = AsConstraints(methodSymbol);
            var body = AsBody(methodSymbol);

            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(returnTypeName), methodName)
                .WithModifiers(modifiers)
                .WithLeadingTrivia(leadingTrivia)
                .WithTypeParameterList(typeParameters)
                .WithParameterList(parameters)
                .WithConstraintClauses(constraints)
                .WithBody(body);
        }

        private SyntaxList<TypeParameterConstraintClauseSyntax> AsConstraints(IMethodSymbol methodSymbol) {
            return SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(
                methodSymbol.TypeParameters.Where(tp =>
                    tp.HasConstructorConstraint || tp.HasReferenceTypeConstraint ||
                    tp.HasValueTypeConstraint || tp.ConstraintTypes.Any()
                ).Select(tp =>
                {
                    var constraint = SyntaxFactory.TypeParameterConstraintClause(tp.Name);

                    if (tp.HasReferenceTypeConstraint)
                    {
                        constraint = constraint.AddConstraints(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint));
                    }

                    if (tp.HasValueTypeConstraint)
                    {
                        constraint = constraint.AddConstraints(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint));
                    }

                    var typeConstraints = tp.ConstraintTypes.Select(symbol => SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName(symbol.GetFullyQualifiedName())));
                    constraint = constraint.AddConstraints(typeConstraints.ToArray());

                    if (tp.HasConstructorConstraint)
                    {
                        constraint = constraint.AddConstraints(SyntaxFactory.ConstructorConstraint());
                    }

                    return constraint;
                })
            );
        }

        private BlockSyntax AsBody(IMethodSymbol methodSymbol)
        {
            var invocation = GetInvocation(methodSymbol);

            var bodyStatement = (methodSymbol.ReturnType.IsSystemVoid()) ?
                (StatementSyntax)SyntaxFactory.ExpressionStatement(invocation) :
                SyntaxFactory.ReturnStatement(invocation);

            return SyntaxFactory.Block(bodyStatement);
        }

        private InvocationExpressionSyntax GetInvocation(IMethodSymbol methodSymbol)
        {
            var name = GetMethodName(methodSymbol);
            var argumentList = GetArgumentList(methodSymbol.Parameters);

            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(ContextFieldName),
                    name
                )
            ).WithArgumentList(argumentList);
        }

        private ArgumentListSyntax GetArgumentList(ImmutableArray<IParameterSymbol> parameterSymbols)
        {
            var arguments = parameterSymbols.Select(GetArgument);

            return SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                    arguments
                )
            );
        }

        private ArgumentSyntax GetArgument(IParameterSymbol parameterSymbol)
        {
            var refKindKeyword = parameterSymbol.RefKind switch
            {
                RefKind.Out => SyntaxKind.OutKeyword,
                RefKind.In => SyntaxKind.InKeyword,
                RefKind.Ref => SyntaxKind.OutKeyword,
                _ => SyntaxKind.None
            };

            return SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameterSymbol.Name))
                .WithRefOrOutKeyword(SyntaxFactory.Token(refKindKeyword));
        }

        private SimpleNameSyntax GetMethodName(IMethodSymbol methodSymbol)
        {
            return methodSymbol.IsGenericMethod ? GetGenericMethodName(methodSymbol) : GetSimpleMethodName(methodSymbol);
        }

        private GenericNameSyntax GetGenericMethodName(IMethodSymbol methodSymbol)
        {
            var typeArguments = AsTypeArgumentList(methodSymbol.TypeArguments);

            return SyntaxFactory.GenericName(methodSymbol.Name)
                .WithTypeArgumentList(typeArguments);
        }

        private SimpleNameSyntax GetSimpleMethodName(IMethodSymbol methodSymbol)
        {
            return SyntaxFactory.IdentifierName(methodSymbol.Name);
        }

        private TypeArgumentListSyntax AsTypeArgumentList(ImmutableArray<ITypeSymbol> typeArguments)
        {
            return SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList<TypeSyntax>(
                    typeArguments.Select(t => SyntaxFactory.IdentifierName(t.ToDisplayString()))
                )
            );
        }

        private PropertyDeclarationSyntax PropertyDeclaration(IPropertySymbol symbol)
        {
            var accessors = new List<AccessorDeclarationSyntax>();
            var hasGetter = symbol.GetMethod != null;
            var hasSetter = symbol.SetMethod != null;

            var propertyName = symbol.Name;
            var propertyTypeName = symbol.Type.ToPrettyString();

            if (hasGetter)
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, propertyName));

            if (hasSetter)
                accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration, propertyName));


            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(propertyTypeName), propertyName)
                .WithModifiers(AsModifierList(SyntaxKind.PublicKeyword))
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
        }

        private AccessorDeclarationSyntax AccessorDeclaration(SyntaxKind kind, string propertyName)
        {
            return SyntaxFactory.AccessorDeclaration(kind)
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(ContextFieldName),
                            SyntaxFactory.IdentifierName(propertyName)
                        )
                    )
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        private static TypeParameterListSyntax AsTypeParameterList(IEnumerable<ITypeParameterSymbol> typeParameterSymbols)
        {
            var typeParameters = SyntaxFactory.TypeParameterList(
                SyntaxFactory.SeparatedList(
                    typeParameterSymbols.Select(tp => SyntaxFactory.TypeParameter(SyntaxFactory.Identifier(tp.Name)))
                )
            );

            if (typeParameters != null && typeParameters.Parameters.Count == 0)
            {
                typeParameters = null;
            }

            return typeParameters;
        }

        private ParameterListSyntax AsParameterList(IEnumerable<IParameterSymbol> parameterSymbols)
        {
            return SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(
                    parameterSymbols.Select(AsParameter)
                )
            );
        }

        private ParameterSyntax AsParameter(IParameterSymbol parameterSymbol)
        {
            var parameterName = parameterSymbol.Name;
            var parameterTypeName = parameterSymbol.Type.ToPrettyString();

            var modifiers = GetModifiers(parameterSymbol);
            var defaultValue = GetDefaultValue(parameterSymbol);

            return SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
                .WithType(SyntaxFactory.ParseTypeName(parameterTypeName))
                .WithModifiers(modifiers)
                .WithDefault(defaultValue);
        }

        private SyntaxTokenList GetModifiers(IParameterSymbol parameterSymbol)
        {
            switch (parameterSymbol.RefKind)
            {
                case RefKind.None:
                    return SyntaxTokenList.Create(SyntaxFactory.Token(SyntaxKind.None));
                case RefKind.Ref:
                    return SyntaxTokenList.Create(
                        SyntaxFactory.Token(SyntaxKind.RefKeyword)
                    );
                case RefKind.Out:
                    return SyntaxTokenList.Create(
                        SyntaxFactory.Token(SyntaxKind.OutKeyword)
                    );
                case RefKind.In:
                    return SyntaxTokenList.Create(
                        SyntaxFactory.Token(SyntaxKind.InKeyword)
                    );
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private EqualsValueClauseSyntax GetDefaultValue(IParameterSymbol parameterSymbol)
        {
            if (!parameterSymbol.HasExplicitDefaultValue)
                return null;

            ExpressionSyntax defaultExpression;
            var explicitDefaultValue = parameterSymbol.ExplicitDefaultValue;

            if (explicitDefaultValue is bool defaultBoolValue)
            {
                defaultExpression = SyntaxFactory.LiteralExpression(
                    defaultBoolValue
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression
                );
            }
            else if (parameterSymbol.ExplicitDefaultValue is string stringDefaultValue)
            {
                defaultExpression = SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(stringDefaultValue)
                );
            }
            else if (parameterSymbol.ExplicitDefaultValue is char charDefaultValue)
            {
                defaultExpression = SyntaxFactory.LiteralExpression(
                    SyntaxKind.CharacterLiteralExpression,
                    SyntaxFactory.Literal(charDefaultValue)
                );
            }
            else if (parameterSymbol.ExplicitDefaultValue == null)
            {
                if (parameterSymbol.Type.IsValueType)
                {
                    defaultExpression = SyntaxFactory.DefaultExpression(
                        SyntaxFactory.IdentifierName(parameterSymbol.Type.ToPrettyString())
                    );
                }
                else
                {
                    defaultExpression = SyntaxFactory.LiteralExpression(
                        SyntaxKind.NullLiteralExpression
                    );
                }
            }
            else
            {
                throw new ArgumentException("Unknown parameter type.");
            }

            return SyntaxFactory.EqualsValueClause(defaultExpression);
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

        private SyntaxTriviaList InheritDoc()
        {
            return SyntaxFactory.ParseLeadingTrivia(
                "///<inheritdoc/>\r\n"
            );
        }
    }
}
