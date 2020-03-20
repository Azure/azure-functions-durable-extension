// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GuidCodeFixProvider)), Shared]
    public class GuidCodeFixProvider: CodeFixProvider
    {
        private static readonly LocalizableString FixGuidInOrchestrator = new LocalizableResourceString(nameof(Resources.FixGuidInOrchestrator), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixDeterministicAttribute = new LocalizableResourceString(nameof(Resources.FixDeterministicAttribute), Resources.ResourceManager, typeof(Resources));

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(GuidAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var expression = root.FindNode(diagnosticSpan) as InvocationExpressionSyntax;
            if (expression == null)
            {
                return;
            }

            if (SyntaxNodeUtils.IsInsideOrchestrator(expression))
            {
                if (CodeFixProviderUtils.TryGetDurableOrchestrationContextVariableName(expression, out string variableName))
                {
                    context.RegisterCodeFix(
                    CodeAction.Create(FixGuidInOrchestrator.ToString(), c => ReplaceWithInvocationExpressionAsync(context.Document, expression, c, variableName + ".NewGuid()"), nameof(GuidCodeFixProvider)),
                    diagnostic);
                }
            }
        }

        private async Task<Document> ReplaceWithInvocationExpressionAsync(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken, string expressionString)
        {
            var newExpression = SyntaxFactory.ParseExpression(expressionString)
                .WithLeadingTrivia(invocationExpression.GetLeadingTrivia())
                .WithTrailingTrivia(invocationExpression.GetTrailingTrivia());

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(invocationExpression, newExpression);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
