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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassNameCodeFixProvider)), Shared]
    public class ClassNameCodeFixProvider : CodeFixProvider
    {
        private static readonly LocalizableString FixEntityFunctionName = new LocalizableResourceString(nameof(Resources.FixEntityFunctionName), Resources.ResourceManager, typeof(Resources));

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

            var attributeArgument = root.FindNode(diagnosticSpan) as AttributeArgumentSyntax;
            if (attributeArgument == null)
            {
                return;
            }

            SemanticModel semanticModel = await context.Document.GetSemanticModelAsync();
            if (SyntaxNodeUtils.TryGetClassSymbol(attributeArgument, semanticModel, out INamedTypeSymbol classSymbol))
            {
                var className = "nameof(" + classSymbol.Name.ToString() + ")";

                context.RegisterCodeFix(
                CodeAction.Create(FixEntityFunctionName.ToString(), cancellationToken => ReplaceAttributeArgumentAsync(context.Document, attributeArgument, cancellationToken, className), nameof(ClassNameCodeFixProvider)),
                diagnostic);
            }
        }

        private static async Task<Document> ReplaceAttributeArgumentAsync(Document document, SyntaxNode attributeArgumentNode, CancellationToken cancellationToken, string expressionString)
        {
            var newAttributeArgument = SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression(expressionString));

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(attributeArgumentNode, newAttributeArgument);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
