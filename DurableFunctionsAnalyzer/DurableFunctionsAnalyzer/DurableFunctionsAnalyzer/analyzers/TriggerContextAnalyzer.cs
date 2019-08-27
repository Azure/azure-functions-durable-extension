using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace DurableFunctionsAnalyzer.analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TriggerContextAnalyzer : OrchestratorAnalyzer
    {
        public const string DiagnosticId = "TriggerContextAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.TriggerContextAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.TriggerContextAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.TriggerContextAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "OrchestratorTriggerAnalyzer";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(FindOrchestrationTriggers, SyntaxKind.Attribute);
        }

        public void FindOrchestrationTriggers(SyntaxNodeAnalysisContext context)
        {
            if (context.Node.ToString() == "OrchestrationTrigger")
            {
                var parameter = context.Node.Parent.Parent;
                var identifierNames = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName) || x.IsKind(SyntaxKind.PredefinedType));
                var paramTypeName = identifierNames.First();
                if (paramTypeName.ToString() != "IDurableOrchestrationContext")
                {
                    var diagnostic = Diagnostic.Create(Rule, paramTypeName.GetLocation(), paramTypeName);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
