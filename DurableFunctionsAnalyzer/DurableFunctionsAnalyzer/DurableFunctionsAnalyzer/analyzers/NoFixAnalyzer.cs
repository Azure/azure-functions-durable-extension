using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace DurableFunctionsAnalyzer.analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NoFixAnalyzer : OrchestratorAnalyzer
    {
        public const string DiagnosticId = "NoFixAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "OrchestratorCodeConstraints";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierIO, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierEnvironmentVariable, SyntaxKind.IdentifierName);
        }
        
        private static void AnalyzeIdentifierIO(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                var identifierText = identifierName.Identifier.ValueText;
                if (IsIOClass(identifierText))
                {
                    if (!OrchestratorUtil.IsInsideOrchestrator(identifierName) || !OrchestratorUtil.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(Rule, identifierName.Identifier.GetLocation(), identifierName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsIOClass(string s)
        {
            return s.Equals("HttpClient") || s.Equals("SqlConnection") || s.Equals("CloudBlobClient") || s.Equals("CloudQueueClinet") || s.Equals("CloudTableClient") || s.Equals("DocumentClient") || s.Equals("WebRequest");
        }
        
        private static void AnalyzeIdentifierEnvironmentVariable(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                if (identifierName.Identifier.ValueText == "GetEnvironmentVariable")
                {
                    var expression = identifierName.Parent;
                    var memberSymbol = context.SemanticModel.GetSymbolInfo(expression).Symbol;

                    if (!memberSymbol?.ToString().StartsWith("System.Environment") ?? true)
                    {
                        return;
                    }
                    else if (!OrchestratorUtil.IsInsideOrchestrator(identifierName) && !OrchestratorUtil.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(Rule, identifierName.Identifier.GetLocation(), identifierName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
