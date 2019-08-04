using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace DurableFunctionsAnalyzer.Analyzers
{
    public class OrchestrationTriggerAnnotationAnalyzer
    {
        public const string DiagnosticId = "DurableFunctionOrchestrationTriggerAnalyzer";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.OrchestrationTriggerAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.OrchestrationTriggerAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.OrchestrationTriggerAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Argument";

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public void FindOrchestrationTriggers(SyntaxNodeAnalysisContext context)
        {
            if (context.Node.ToString() == "OrchestrationTrigger")
            {
                var parameter = context.Node.Parent.Parent;
                var identifierNames = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                if(!identifierNames.Any() || (identifierNames.First().ToString()!= "DurableOrchestrationContext" && identifierNames.First().ToString() != "DurableOrchestrationContextBase"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
                }
            }
        }
    }
}
