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
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        private List<EntityInterface> entityInterfacesList = new List<EntityInterface>();
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                  Rule,
                  InterfaceContentAnalyzer.NoMethodsRule,
                  InterfaceContentAnalyzer.NotAMethodRule,
                  ParameterAnalyzer.Rule,
                  EntityInterfaceReturnTypeAnalyzer.Rule);
            }
        }

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
            foreach (EntityInterface entityInterface in entityInterfacesList)
            {
                InterfaceContentAnalyzer.ReportProblems(context, entityInterface);
                ParameterAnalyzer.ReportProblems(context, entityInterface);
                EntityInterfaceReturnTypeAnalyzer.ReportProblems(context, entityInterface);
            }
        }

        public void FindEntityCalls(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is MemberAccessExpressionSyntax expression &&
                SyntaxNodeUtils.IsInsideFunction(context.SemanticModel, expression))
            {
                var name = expression.Name;
                if (name.ToString().StartsWith("SignalEntityAsync"))
                {
                    if (!SyntaxNodeUtils.TryGetTypeArgumentIdentifier(expression, out SyntaxNode typeArgument))
                    {
                        return;
                    }
                    
                    if (TryFindEntityInterface(context, typeArgument, out EntityInterface entityInterface))
                    {
                        entityInterfacesList.Add(entityInterface);
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(Rule, typeArgument.GetLocation(), typeArgument);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private bool TryFindEntityInterface(SyntaxNodeAnalysisContext context, SyntaxNode identifierName, out EntityInterface entityInterface)
        {
            if (SyntaxNodeUtils.TryGetDeclaredSyntaxNode(context.SemanticModel, identifierName, out SyntaxNode declaration))
            {
                if (IsInterface(declaration))
                {
                    entityInterface = new EntityInterface { Name = identifierName.ToString(), InterfaceDeclaration = declaration };
                    return true;
                }
            }

            entityInterface = null;
            return false;
        }

        private bool IsInterface(SyntaxNode declaration)
        {
            var interfaceKeyword = declaration.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.InterfaceKeyword));
            return interfaceKeyword != null && !interfaceKeyword.IsKind(SyntaxKind.None);
        }
    }
}
