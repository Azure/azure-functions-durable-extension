// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClientCodeFixProvider)), Shared]
    public class ClientCodeFixProvider : CodeFixProvider
    {
        private static readonly LocalizableString FixIDurableClient = new LocalizableResourceString(nameof(Resources.FixIDurableClient), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixIDurableEntityClient = new LocalizableResourceString(nameof(Resources.FixIDurableEntityClient), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixIDurableOrchestrationClient = new LocalizableResourceString(nameof(Resources.FixIDurableOrchestrationClient), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixDurableOrchestrationClient = new LocalizableResourceString(nameof(Resources.FixDurableOrchestrationClient), Resources.ResourceManager, typeof(Resources));

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ClientAnalyzer.DiagnosticId); }
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
            var durableVersion = SyntaxNodeUtils.GetDurableVersion(semanticModel);

            if (durableVersion.Equals(DurableVersion.V1))
            {
                context.RegisterCodeFix(
                CodeAction.Create(FixDurableOrchestrationClient.ToString(), cancellationToken => CodeFixProviderUtils.ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "DurableOrchestrationClient"), nameof(ClientCodeFixProvider) + nameof(FixDurableOrchestrationClient)),
                diagnostic);
            }
            else if (durableVersion.Equals(DurableVersion.V2))
            {
                context.RegisterCodeFix(
                CodeAction.Create(FixIDurableClient.ToString(), cancellationToken => CodeFixProviderUtils.ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableClient"), nameof(ClientCodeFixProvider) + nameof(FixIDurableClient)),
                diagnostic);

                context.RegisterCodeFix(
                CodeAction.Create(FixIDurableEntityClient.ToString(), cancellationToken => CodeFixProviderUtils.ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableEntityClient"), nameof(ClientCodeFixProvider) + nameof(FixIDurableEntityClient)),
                diagnostic);

                context.RegisterCodeFix(
                CodeAction.Create(FixIDurableOrchestrationClient.ToString(), cancellationToken => CodeFixProviderUtils.ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableOrchestrationClient"), nameof(ClientCodeFixProvider) + nameof(FixIDurableOrchestrationClient)),
                diagnostic);
            }
        }
    }
}
