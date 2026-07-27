// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
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

        public static bool RegisterDiagnostic(CompilationAnalysisContext context, SemanticModel semanticModel, SyntaxNode method)
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
                        && memberAccessExpression.Name == identifierName
                        && memberAccessExpression.Parent is InvocationExpressionSyntax invocationExpression
                        && IsAwaited(invocationExpression, method, semanticModel)
                        && !ContinuesOnCapturedContext(invocationExpression, semanticModel))
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

        private static bool IsAwaited(
            InvocationExpressionSyntax invocationExpression,
            SyntaxNode method,
            SemanticModel semanticModel)
        {
            ExpressionSyntax expression = GetOutermostParenthesizedExpression(invocationExpression);
            if (IsDirectlyAwaited(expression))
            {
                return true;
            }

            if (expression.Parent is AssignmentExpressionSyntax directAssignment &&
                directAssignment.Right == expression &&
                IsDirectlyAwaited(directAssignment))
            {
                return true;
            }

            if (!TryGetCapturedSymbol(expression, semanticModel, out ISymbol capturedSymbol))
            {
                return false;
            }

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is AwaitExpressionSyntax laterAwait &&
                    laterAwait.SpanStart > invocationExpression.SpanStart)
                {
                    ExpressionSyntax awaitedExpression = laterAwait.Expression;
                    while (awaitedExpression is ParenthesizedExpressionSyntax parenthesizedExpression)
                    {
                        awaitedExpression = parenthesizedExpression.Expression;
                    }

                    if (SyntaxNodeUtils.TryGetISymbol(semanticModel, awaitedExpression, out ISymbol awaitedSymbol) &&
                        SymbolEqualityComparer.Default.Equals(capturedSymbol, awaitedSymbol))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsDirectlyAwaited(ExpressionSyntax expression)
        {
            expression = GetOutermostParenthesizedExpression(expression);
            return expression.Parent is AwaitExpressionSyntax awaitExpression &&
                awaitExpression.Expression == expression;
        }

        private static ExpressionSyntax GetOutermostParenthesizedExpression(ExpressionSyntax expression)
        {
            while (expression.Parent is ParenthesizedExpressionSyntax parenthesizedExpression &&
                parenthesizedExpression.Expression == expression)
            {
                expression = parenthesizedExpression;
            }

            return expression;
        }

        private static bool TryGetCapturedSymbol(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            out ISymbol capturedSymbol)
        {
            if (expression.Parent is EqualsValueClauseSyntax equalsValueClause &&
                equalsValueClause.Value == expression &&
                equalsValueClause.Parent is VariableDeclaratorSyntax variableDeclarator)
            {
                capturedSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator);
                return capturedSymbol != null;
            }

            if (expression.Parent is AssignmentExpressionSyntax assignmentExpression &&
                assignmentExpression.Right == expression)
            {
                return SyntaxNodeUtils.TryGetISymbol(
                    semanticModel,
                    assignmentExpression.Left,
                    out capturedSymbol);
            }

            capturedSymbol = null;
            return false;
        }

        // A compile-time constant true preserves the orchestration SynchronizationContext (identical to a normal
        // await), so it is safe. Only the standard one-argument form is exempted; additional arguments indicate a
        // different overload whose behavior cannot be proven safe.
        private static bool ContinuesOnCapturedContext(
            InvocationExpressionSyntax invocationExpression,
            SemanticModel semanticModel)
        {
            SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count != 1)
            {
                return false;
            }

            Optional<object> constantValue = semanticModel.GetConstantValue(arguments[0].Expression);
            return constantValue.HasValue &&
                constantValue.Value is bool continueOnCapturedContext &&
                continueOnCapturedContext;
        }
    }
}
