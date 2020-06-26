// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TimerAnalyzer
    {
        public const string DiagnosticId = "DF0103";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.TimerAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V2MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V1MessageFormat = new LocalizableResourceString(nameof(Resources.V1TimerAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor V1Rule = new DiagnosticDescriptor(DiagnosticId, Title, V1MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor V2Rule = new DiagnosticDescriptor(DiagnosticId, Title, V2MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        private static DurableVersion version;

        internal static bool RegisterDiagnostic(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            // | is the non short circuit or; this is important so that each method analyzes the code and reports all needed diagnostics.
            return (AnalyzeIdentifierTask(method, context, semanticModel) |
                AnalyzeIdentifierThread(method, context, semanticModel));
        }

        private static bool AnalyzeIdentifierTask(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    version = SyntaxNodeUtils.GetDurableVersion(semanticModel);

                    var identifierText = identifierName.Identifier.ValueText;
                    if (identifierText == "Delay")
                    {
                        var memberAccessExpression = identifierName.Parent;
                        if (SyntaxNodeUtils.TryGetISymbol(semanticModel, memberAccessExpression, out ISymbol memberSymbol))
                        {
                            if (memberSymbol != null && memberSymbol.ToString().StartsWith("System.Threading.Tasks.Task"))
                            {
                                if (TryGetRuleFromVersion(out DiagnosticDescriptor rule))
                                {
                                    var expression = GetAwaitOrInvocationExpression(memberAccessExpression);

                                    var diagnostic = Diagnostic.Create(rule, expression.GetLocation(), expression);

                                    context.ReportDiagnostic(diagnostic);

                                    diagnosedIssue = true;
                                }
                            }
                        }
                    }
                }
            }

            return diagnosedIssue;
        }

        private static SyntaxNode GetAwaitOrInvocationExpression(SyntaxNode memberAccessExpression)
        {
            var invocationExpression = memberAccessExpression.Parent;
            var awaitExpression = invocationExpression.Parent;
            if (awaitExpression.IsKind(SyntaxKind.AwaitExpression))
            {
                return awaitExpression;
            }

            return invocationExpression;
        }

        private static bool AnalyzeIdentifierThread(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    version = SyntaxNodeUtils.GetDurableVersion(semanticModel);

                    var identifierText = identifierName.Identifier.ValueText;
                    if (identifierText == "Sleep")
                    {
                        var memberAccessExpression = identifierName.Parent;
                        if (SyntaxNodeUtils.TryGetISymbol(semanticModel, memberAccessExpression, out ISymbol memberSymbol))
                        {
                            if (memberSymbol != null && memberSymbol.ToString().StartsWith("System.Threading.Thread"))
                            {
                                if (TryGetRuleFromVersion(out DiagnosticDescriptor rule))
                                {
                                    var expression = GetAwaitOrInvocationExpression(memberAccessExpression);

                                    var diagnostic = Diagnostic.Create(rule, expression.GetLocation(), expression);

                                    context.ReportDiagnostic(diagnostic);

                                    diagnosedIssue = true;
                                }
                            }
                        }
                    }
                }
            }

            return diagnosedIssue;
        }

        private static bool TryGetRuleFromVersion(out DiagnosticDescriptor rule)
        {
            if (version.Equals(DurableVersion.V1))
            {
                rule = V1Rule;
                return true;
            }
            else if (version.Equals(DurableVersion.V2))
            {
                rule = V2Rule;
                return true;
            }

            rule = null;
            return false;
        }
    }
}
