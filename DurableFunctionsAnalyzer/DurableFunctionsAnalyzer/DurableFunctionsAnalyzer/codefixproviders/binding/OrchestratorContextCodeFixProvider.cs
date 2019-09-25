// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using DurableFunctionsAnalyzer.analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DurableFunctionsAnalyzer.codefixproviders
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OrchestratorContextCodeFixProvider)), Shared]
    public class OrchestratorContextCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private const string title = "OrchestratorContextFix";

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
                    CodeAction.Create("Replace with DurableOrchestrationContext", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "DurableOrchestrationContext"), OrchestratorContextAnalyzer.DiagnosticId),
                    diagnostic);

                context.RegisterCodeFix(
                    CodeAction.Create("Replace with DurableOrchestrationContextBase", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "DurableOrchestrationContextBase"), OrchestratorContextAnalyzer.DiagnosticId),
                    diagnostic);
            }
            else if (durableVersion.Equals(DurableVersion.V2))
            {
                context.RegisterCodeFix(
                    CodeAction.Create("Replace with IDurableOrchestrationContext", cancellationToken => ReplaceWithIdentifierAsync(context.Document, identifierNode, cancellationToken, "IDurableOrchestrationContext"), OrchestratorContextAnalyzer.DiagnosticId),
                    diagnostic);
            }
        }
    }
}
