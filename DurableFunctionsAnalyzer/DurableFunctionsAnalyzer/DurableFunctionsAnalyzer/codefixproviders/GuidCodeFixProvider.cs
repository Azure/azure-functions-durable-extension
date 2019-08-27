using DurableFunctionsAnalyzer.analyzers;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer.codefixproviders
{
    public class GuidCodeFixProvider: OrchestratorCodeFixProvider
    {
        private const string title = "Replace with context.NewGuid";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(GuidAnalyzer.DiagnosticId); }
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

            if (OrchestratorUtil.IsInsideOrchestrator(identifierNode))
            {
                var contextName = GetContextVariableName(identifierNode);

                context.RegisterCodeFix(
                CodeAction.Create("Replace with (IDurableOrchestrationContext).NewGuid", c => ReplaceWithExpression(context.Document, identifierNode, c, contextName + ".NewGuid")),
                diagnostic);
            }
            else if (OrchestratorUtil.IsMarkedDeterministic(identifierNode))
            {
                context.RegisterCodeFix(
                CodeAction.Create("Remove IsDeterministic Attribute", c => RemoveIsDeterministicAttributeAsync(context.Document, identifierNode, c)), diagnostic);
            }
}
    }
}
