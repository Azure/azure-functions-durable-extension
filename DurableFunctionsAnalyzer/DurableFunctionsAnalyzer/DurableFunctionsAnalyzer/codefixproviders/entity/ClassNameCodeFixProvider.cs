﻿// Copyright (c) .NET Foundation. All rights reserved.
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassNameCodeFixProvider)), Shared]
    public class ClassNameCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private const string title = "EntityContextFix";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ClassNameAnalyzer.DiagnosticId); }
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
            SemanticModel semanticModel = await context.Document.GetSemanticModelAsync();
            if (SyntaxNodeUtils.TryGetClassSymbol(identifierNode, semanticModel, out INamedTypeSymbol classSymbol))
            {
                var className = classSymbol.Name.ToString();

                context.RegisterCodeFix(
                CodeAction.Create("Replace with Entity Class Name", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, className)),
                diagnostic);
            }
        }
    }
}
