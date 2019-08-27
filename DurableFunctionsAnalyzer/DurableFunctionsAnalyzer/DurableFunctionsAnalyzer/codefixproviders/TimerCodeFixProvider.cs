using DurableFunctionsAnalyzer.analyzers;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer.codefixproviders
{
    public class TimerCodeFixProvider : OrchestratorCodeFixProvider
    {
        private const string title = "Replace with context.CreateTimer";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(TimerAnalyzer.DiagnosticId); }
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
                CodeAction.Create("Replace with (IDurableOrchestrationContext).CreateTimer", c => ReplaceWithExpression(context.Document, identifierNode, c, contextName + ".CreateTimer")),
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
