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
    [DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
    public class InterfaceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0301";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.SignalEntityAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.SignalEntityAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.SignalEntityAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.EntityInterface;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        private List<EntityInterface> entityInterfacesList = new List<EntityInterface>();
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, InterfaceContentAnalyzer.NoMethodsRule, InterfaceContentAnalyzer.NotAMethodRule, ParameterAnalyzer.Rule, EntityInterfaceReturnTypeAnalyzer.Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
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
            EntityInterfaceReturnTypeAnalyzer returnTypeAnalyzer = new EntityInterfaceReturnTypeAnalyzer();

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
                    if (TryGetTypeArgumentList(expression, out SyntaxNode typeArgumentList))
                    {
                        if (!TryGetIdentifierName(typeArgumentList, out SyntaxNode identifierName))
                        {
                            var diagnosticWrongType = Diagnostic.Create(Rule, typeArgumentList.GetLocation(), typeArgumentList);

                            context.ReportDiagnostic(diagnosticWrongType);

                            return;
                        }

                        if (TryFindEntityInterface(identifierName, context, out EntityInterface entityInterface))
                        {
                            entityInterfacesList.Add(entityInterface);
                        }
                        else
                        {
                            var diagnostic = Diagnostic.Create(Rule, identifierName.GetLocation(), identifierName);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private bool TryFindEntityInterface(SyntaxNode identifierName, SyntaxNodeAnalysisContext context, out EntityInterface entityInterface)
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

                            entityInterface = new EntityInterface { name = identifierName.ToString(), InterfaceDeclaration = declaration, typeSymbol = interfaceType };
                            return true;
                        }
                    }
                }
            }

            entityInterface = null;
            return false;
        }

        private bool TryGetIdentifierName(SyntaxNode typeArgumentList, out SyntaxNode identifierName)
        {
            identifierName = typeArgumentList.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName)).FirstOrDefault();
            return identifierName != null;
        }

        private bool TryGetTypeArgumentList(MemberAccessExpressionSyntax expression, out SyntaxNode typeArgumentList)
        {
            var genericNameEnumerable = expression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName));
            if (genericNameEnumerable.Any())
            {
                //TypeArgumentList will always exist inside a GenericName
                typeArgumentList = genericNameEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList)).First();
                return true;
            }

            typeArgumentList = null;
            return false;
        }
    }
}
