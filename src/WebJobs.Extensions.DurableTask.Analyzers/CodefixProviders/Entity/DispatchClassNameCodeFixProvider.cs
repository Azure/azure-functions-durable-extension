// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassNameCodeFixProvider)), Shared]
    public class DispatchClassNameCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private static readonly LocalizableString FixDispatchEntityCall = new LocalizableResourceString(nameof(Resources.FixEntityFunctionName), Resources.ResourceManager, typeof(Resources));

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DispatchClassNameAnalyzer.DiagnosticId); }
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
                CodeAction.Create(FixDispatchEntityCall.ToString(), cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, className), nameof(DispatchClassNameCodeFixProvider)),
                diagnostic);
            }
        }

        protected async Task<Document> ReplaceTypeArgumentAsync(Document document, SyntaxNode attributeArgumentNode, CancellationToken cancellationToken, string expression)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(attributeArgumentNode, SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression(expression)));
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
