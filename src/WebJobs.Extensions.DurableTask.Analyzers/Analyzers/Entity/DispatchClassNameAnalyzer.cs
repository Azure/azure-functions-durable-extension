// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
    public class DispatchClassNameAnalyzer: DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0307";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DispatchClassNameAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DispatchClassNameAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString IncorrectTypeMessageFormat = new LocalizableResourceString(nameof(Resources.DispatchClassNameAnalyzerIncorrectTypeMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DispatchClassNameAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Entity;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);
        private static DiagnosticDescriptor IncorrectTypeRule = new DiagnosticDescriptor(DiagnosticId, Title, IncorrectTypeMessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule, IncorrectTypeRule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeDispatchEntityName, SyntaxKind.SimpleMemberAccessExpression);
        }

        private void AnalyzeDispatchEntityName(SyntaxNodeAnalysisContext context)
        {
            var expression = context.Node as MemberAccessExpressionSyntax;
            if (expression != null)
            {
                var name = expression.Name;
                if (name.ToString().StartsWith("DispatchAsync"))
                {
                    if (TryGetTypeArgumentList(expression, out SyntaxNode typeArgumentList))
                    {
                        if(!TryGetIdentifierName(out SyntaxNode identifierName, typeArgumentList))
                        {
                            var diagnostic = Diagnostic.Create(IncorrectTypeRule, typeArgumentList.GetLocation(), typeArgumentList);

                            context.ReportDiagnostic(diagnostic);

                            return;
                        }
                        else
                        {
                            if (SyntaxNodeUtils.TryGetClassSymbol(expression, context.SemanticModel, out INamedTypeSymbol classSymbol))
                            {
                                var className = classSymbol.Name.ToString();

                                if (!string.Equals(className, identifierName.ToString()))
                                {
                                    var diagnostic = Diagnostic.Create(Rule, identifierName.GetLocation(), identifierName, className);

                                    context.ReportDiagnostic(diagnostic);
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool TryGetTypeArgumentList(MemberAccessExpressionSyntax expression, out SyntaxNode typeArgumentList)
        {
            var genericNameEnumerable = expression.ChildNodes().Where(x => x.IsKind(SyntaxKind.GenericName));
            if (genericNameEnumerable.Any())
            {
                typeArgumentList = genericNameEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.TypeArgumentList)).First();
                return true;
            }

            typeArgumentList = null;
            return false;
        }

        private bool TryGetIdentifierName(out SyntaxNode identifierName, SyntaxNode typeArgumentList)
        {
            var identifierNameEnumerable = typeArgumentList.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
            if (identifierNameEnumerable.Any())
            {
                identifierName = identifierNameEnumerable.First();
                return true;
            }

            identifierName = null;
            return false;
        }
    }
}
