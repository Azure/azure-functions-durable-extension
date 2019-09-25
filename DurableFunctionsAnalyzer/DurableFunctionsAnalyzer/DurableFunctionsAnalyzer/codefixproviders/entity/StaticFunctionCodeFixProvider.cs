// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableFunctionsAnalyzer.analyzers.entity;
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

namespace DurableFunctionsAnalyzer.codefixproviders
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StaticFunctionCodeFixProvider)), Shared]
    class StaticFunctionCodeFixProvider: DurableFunctionsCodeFixProvider
    {
        private const string title = "Static function codeFix";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(StaticFunctionAnalyzer.DiagnosticId); }
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
            CodeAction.Create("Make method static", cancellationToken => AddStaticModifierAsync(context.Document, identifierNode, cancellationToken)),
            diagnostic);
        }

        private async Task<Document> AddStaticModifierAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (SyntaxNodeUtils.TryGetMethodDeclaration(out SyntaxNode methodDeclaration, identifierNode))
            {
                var newMethodDeclaration = ((MethodDeclarationSyntax)methodDeclaration).AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                var newRoot = root.ReplaceNode(methodDeclaration, newMethodDeclaration);

                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }
    }
}
