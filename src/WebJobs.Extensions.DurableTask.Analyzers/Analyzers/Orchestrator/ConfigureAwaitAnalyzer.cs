// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ConfigureAwaitAnalyzer
    {
        public const string DiagnosticId = "DF0114";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ConfigureAwaitAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static bool RegisterDiagnostic(CompilationAnalysisContext context, SyntaxNode method)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    var identifierText = identifierName.Identifier.ValueText;

                    // ConfigureAwait(false) moves the continuation off the orchestration SynchronizationContext
                    // and breaks deterministic replay. It is detected syntactically (like Task.ContinueWith) so it
                    // is caught even when the awaited task is an activity or sub-orchestrator call whose type may
                    // not fully bind during analysis.
                    if (identifierText == "ConfigureAwait"
                        && identifierName.Parent is MemberAccessExpressionSyntax memberAccessExpression
                        && memberAccessExpression.Parent is InvocationExpressionSyntax invocationExpression
                        && !ContinuesOnCapturedContext(invocationExpression))
                    {
                        var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), "ConfigureAwait");

                        if (context.Compilation.ContainsSyntaxTree(method.SyntaxTree))
                        {
                            context.ReportDiagnostic(diagnostic);
                        }

                        diagnosedIssue = true;
                    }
                }
            }

            return diagnosedIssue;
        }

        // ConfigureAwait(true) preserves the orchestration SynchronizationContext (identical to a normal await),
        // so it is safe. Only ConfigureAwait(false) - or a non-literal argument we cannot prove is true - is flagged.
        private static bool ContinuesOnCapturedContext(InvocationExpressionSyntax invocationExpression)
        {
            foreach (ArgumentSyntax argument in invocationExpression.ArgumentList.Arguments)
            {
                if (argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
