// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableFunctionsAnalyzer.analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer.codefixproviders
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DateTimeCodeFixProvider)), Shared]
    public class DateTimeCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private const string title = "Replace with context.CurrentUtcDateTime";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DateTimeAnalyzer.DiagnosticId); }
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

            var expression = root.FindNode(diagnosticSpan);

            if (SyntaxNodeUtils.IsInsideOrchestrator(expression))
            {
                var contextName = GetDurableOrchestrationContextVariableName(expression);

                context.RegisterCodeFix(
                CodeAction.Create("Replace with (IDurableOrchestrationContext).CurrentUtcDateTime", c => ReplaceWithIdentifierAsync(context.Document, expression, c, contextName + ".CurrentUtcDateTime")), diagnostic);
            }
            else if (SyntaxNodeUtils.IsMarkedDeterministic(expression))
            {
                context.RegisterCodeFix(
                CodeAction.Create("Remove Deterministic Attribute", c => RemoveDeterministicAttributeAsync(context.Document, expression, c)), diagnostic);
            }
        }
    }
}
