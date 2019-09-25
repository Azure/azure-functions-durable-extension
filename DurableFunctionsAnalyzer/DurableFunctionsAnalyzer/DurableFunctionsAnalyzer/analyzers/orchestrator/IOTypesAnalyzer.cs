// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace DurableFunctionsAnalyzer.analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IOTypesAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0105";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.IOTypesAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "OrchestratorCodeConstraints";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierIO, SyntaxKind.IdentifierName);
        }
        
        private static void AnalyzeIdentifierIO(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                var identifierText = identifierName.Identifier.ValueText;
                var identifierNameSymbol = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol;
                var typeInfo = context.SemanticModel.GetTypeInfo(identifierName);
                if (typeInfo.Type != null)
                {
                    var type = typeInfo.Type.ToString();
                    if (IsIOClass(identifierText) || IsIOClass(type))
                    {
                        if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
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

        private static bool IsIOClass(string s)
        {
            return s.Contains("HttpClient") || s.Contains("SqlConnection") || s.Contains("CloudBlobClient") || s.Contains("CloudQueueClient") || s.Contains("CloudTableClient") || s.Contains("DocumentClient") || s.Contains("WebRequest");
        }
    }
}
