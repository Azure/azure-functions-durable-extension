// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OrchestratorContextCodeFixProvider)), Shared]
    public class OrchestratorContextCodeFixProvider : CodeFixProvider
    {
        private static readonly LocalizableString FixDurableOrchestrationContext = new LocalizableResourceString(nameof(Resources.FixDurableOrchestrationContext), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixDurableOrchestrationContextBase = new LocalizableResourceString(nameof(Resources.FixDurableOrchestrationContextBase), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixIDurableOrchestrationContext = new LocalizableResourceString(nameof(Resources.FixIDurableOrchestrationContext), Resources.ResourceManager, typeof(Resources));

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(OrchestratorContextAnalyzer.DiagnosticId); }
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
                    CodeAction.Create(FixDurableOrchestrationContext.ToString(), cancellationToken => CodeFixProviderUtils.ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "DurableOrchestrationContext"), nameof(OrchestratorContextCodeFixProvider) + nameof(FixDurableOrchestrationContext)),
                    diagnostic);

                context.RegisterCodeFix(
                    CodeAction.Create(FixDurableOrchestrationContextBase.ToString(), cancellationToken => CodeFixProviderUtils.ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "DurableOrchestrationContextBase"), nameof(OrchestratorContextCodeFixProvider) + nameof(FixDurableOrchestrationContextBase)),
                    diagnostic);
            }
            else if (durableVersion.Equals(DurableVersion.V2))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(FixIDurableOrchestrationContext.ToString(), cancellationToken => CodeFixProviderUtils.ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableOrchestrationContext"), nameof(OrchestratorContextCodeFixProvider) + nameof(FixIDurableOrchestrationContext)),
                    diagnostic);
            }
        }
    }
}
