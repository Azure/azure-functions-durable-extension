// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EntityContextCodeFixProvider)), Shared]
    public class EntityContextCodeFixProvider: DurableFunctionsCodeFixProvider
    {
        private const string title = "EntityContextFix";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(EntityContextAnalyzer.DiagnosticId); }
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
            CodeAction.Create("Replace with IDurableEntityContext", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableEntityContext"), EntityContextAnalyzer.DiagnosticId),
            diagnostic);
        }
    }
}
