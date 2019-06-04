using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NewGuidAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NewGuidAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.NewGuidTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.NewGuidMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.NewGuidDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodCalls, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeMethodCalls(SyntaxNodeAnalysisContext context)
        {
            //TODO
            //var diagnostic = Diagnostic.Create(Rule, context., namedTypeSymbol.Name);

            //context.ReportDiagnostic(diagnostic);
        }
    }
}
