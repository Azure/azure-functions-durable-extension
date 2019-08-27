using DurableFunctionsAnalyzer.analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer.codefixproviders
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DateTimeCodeFixProvider)), Shared]
    public class DateTimeCodeFixProvider : OrchestratorCodeFixProvider
    {
        private const string title = "Replace with context.CurrentUtcDateTime";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DateTimeAnalyzer.DiagnosticId); }
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
                CodeAction.Create("Replace with (IDurableOrchestrationContext).CurrentUtcDateTime", c => ReplaceWithExpression(context.Document, identifierNode, c, contextName + ".CurrentUtcDateTime")), diagnostic);
            }
            else if (OrchestratorUtil.IsMarkedDeterministic(identifierNode))
            {
                context.RegisterCodeFix(
                CodeAction.Create("Remove IsDeterministic Attribute", c => RemoveIsDeterministicAttributeAsync(context.Document, identifierNode, c)), diagnostic);
            }
        }
    }
}
