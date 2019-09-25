// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableFunctionsAnalyzer.analyzers.entityInterface;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DurableFunctionsAnalyzer.analyzers.entity
{
    [DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
    public class InterfaceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0301";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.SignalEntityAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.SignalEntityAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.SignalEntityAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Entity";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        List<EntityInterface> entityInterfacesList = new List<EntityInterface>();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule, InterfaceContentAnalyzer.NoMethodsRule, InterfaceContentAnalyzer.NotAMethodRule, ParameterAnalyzer.Rule, ReturnTypeAnalyzer.Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            InterfaceAnalyzer interfaceAnalyzer = new InterfaceAnalyzer();

            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(interfaceAnalyzer.FindEntityCalls, SyntaxKind.SimpleMemberAccessExpression);

                compilation.RegisterCompilationEndAction(interfaceAnalyzer.RegisterAnalyzers);
            });
        }


        private void RegisterAnalyzers(CompilationAnalysisContext context)
        {
            InterfaceContentAnalyzer contentAnalyzer = new InterfaceContentAnalyzer();
            ParameterAnalyzer parameterAnalyzer = new ParameterAnalyzer();
            ReturnTypeAnalyzer returnTypeAnalyzer = new ReturnTypeAnalyzer();

            foreach (EntityInterface entityInterface in entityInterfacesList)
            {
                contentAnalyzer.ReportProblems(context, entityInterface);
                parameterAnalyzer.ReportProblems(context, entityInterface);
                returnTypeAnalyzer.ReportProblems(context, entityInterface);
            }
        }

        public void FindEntityCalls(SyntaxNodeAnalysisContext context)
        {
            var expression = context.Node as MemberAccessExpressionSyntax;
            if (expression != null)
            {
                var name = expression.Name;
                if (name.ToString().StartsWith("SignalEntityAsync"))
                {
                    if (TryGetIdentifierNameOrProduceDiagnostic(out SyntaxNode identifierName, expression, context))
                    {
                        if (!TryFindAndStoreEntityInterface(identifierName, context))
                        {
                            var diagnostic = Diagnostic.Create(Rule, identifierName.GetLocation(), identifierName);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private bool TryFindAndStoreEntityInterface(SyntaxNode identifierName, SyntaxNodeAnalysisContext context)
        {
            var interfaceSymbol = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol;
            if (interfaceSymbol != null)
            {
                var syntaxReference = interfaceSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxReference != null)
                {
                    var declaration = syntaxReference.GetSyntax(context.CancellationToken);
                    if (declaration != null)
                    {
                        var interfaceKeyword = declaration.ChildTokens().Where(x => x.IsKind(SyntaxKind.InterfaceKeyword));
                        if (interfaceKeyword.Any())
                        {
                            var interfaceType = context.SemanticModel.GetTypeInfo(declaration).Type;

                            EntityInterface entityInterface = new EntityInterface { name = identifierName.ToString(), InterfaceDeclaration = declaration, typeSymbol = interfaceType };
                            entityInterfacesList.Add(entityInterface);

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryGetIdentifierNameOrProduceDiagnostic(out SyntaxNode identifierName, SyntaxNode expression, SyntaxNodeAnalysisContext context)
        {
            var genericNameEnumerable = expression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName));
            if (genericNameEnumerable.Any())
            {
                var typeArgumentList = genericNameEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList)).First();
                var identifierNameEnumerable = typeArgumentList.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                if (identifierNameEnumerable.Any())
                {
                    identifierName = identifierNameEnumerable.First();
                    return true;
                }
                else
                {
                    var diagnosticWrongType = Diagnostic.Create(Rule, typeArgumentList.GetLocation(), typeArgumentList);

                    context.ReportDiagnostic(diagnosticWrongType);

                    identifierName = null;
                    return false;
                }
            }

            identifierName = null;
            return false;
        }
    }
}
