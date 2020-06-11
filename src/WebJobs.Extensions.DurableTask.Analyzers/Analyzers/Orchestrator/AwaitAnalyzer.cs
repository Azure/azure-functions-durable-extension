using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AwaitAnalyzer
    {
        public const string DiagnosticId = "DF0108";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AwaitAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AwaitAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static bool RegisterDiagnostic(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant.IsKind(SyntaxKind.AwaitExpression))
                {
                    var invocationExpression = descendant.ChildNodes().Where(x => x.IsKind(SyntaxKind.InvocationExpression)).FirstOrDefault();
                    if (invocationExpression != null)
                    {
                        if (SyntaxNodeUtils.GetSyntaxTreeSemanticModel(semanticModel, invocationExpression).GetSymbolInfo(invocationExpression).Symbol is IMethodSymbol methodSymbol)
                        {
                            var syntaxReference = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                            if (syntaxReference != null)
                            {
                                var diagnostic = Diagnostic.Create(Rule, descendant.GetLocation(), invocationExpression);

                                context.ReportDiagnostic(diagnostic);

                                diagnosedIssue = true;
                            }
                        }
                    }
                }
            }

            return diagnosedIssue;
        }
    }
}
