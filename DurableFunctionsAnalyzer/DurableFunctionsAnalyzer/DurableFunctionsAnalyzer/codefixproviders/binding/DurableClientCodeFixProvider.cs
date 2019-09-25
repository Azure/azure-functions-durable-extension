// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableFunctionsAnalyzer.analyzers.binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer.codefixproviders.binding
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DurableClientCodeFixProvider)), Shared]
    class DurableClientCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private const string title = "DurableClientFix";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DurableClientAnalyzer.DiagnosticId); }
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

            var identifierNode = root.FindNode(diagnosticSpan);

            context.RegisterCodeFix(
            CodeAction.Create("Replace with IDurableClient", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableClient")),
            diagnostic);

            context.RegisterCodeFix(
            CodeAction.Create("Replace with IDurableEntityClient", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableEntityClient")),
            diagnostic);

            context.RegisterCodeFix(
            CodeAction.Create("Replace with IDurableOrchestrationClient", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableOrchestrationClient")),
            diagnostic);
        }
    }
}
