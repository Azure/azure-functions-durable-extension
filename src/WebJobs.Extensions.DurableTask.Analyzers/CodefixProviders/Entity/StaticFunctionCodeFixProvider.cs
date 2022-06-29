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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StaticFunctionCodeFixProvider)), Shared]
    public class StaticFunctionCodeFixProvider: CodeFixProvider
    {
        private static readonly LocalizableString FixEntityFunctionStaticModifier = new LocalizableResourceString(nameof(Resources.FixEntityFunctionStaticModifier), Resources.ResourceManager, typeof(Resources));

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
            CodeAction.Create(FixEntityFunctionStaticModifier.ToString(), cancellationToken => AddStaticModifierAsync(context.Document, identifierNode, cancellationToken), nameof(StaticFunctionCodeFixProvider)),
            diagnostic);
        }

        private async Task<Document> AddStaticModifierAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (SyntaxNodeUtils.TryGetMethodDeclaration(identifierNode, out MethodDeclarationSyntax methodDeclaration))
            {
                var newMethodDeclaration = methodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                var newRoot = root.ReplaceNode(methodDeclaration, newMethodDeclaration);

                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }
    }
}
