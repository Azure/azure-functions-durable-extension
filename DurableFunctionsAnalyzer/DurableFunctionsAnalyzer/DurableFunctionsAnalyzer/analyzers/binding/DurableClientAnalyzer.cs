// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace DurableFunctionsAnalyzer.analyzers.binding
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    class DurableClientAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0203";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DurableClientAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DurableClientAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DurableClientAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "DurableClientAnalyzer";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Error;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(FindEntityTriggers, SyntaxKind.Attribute);
        }

        public void FindEntityTriggers(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;

            if (string.Equals(attribute.ToString(), "DurableClient"))
            {
                if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(out SyntaxNode parameterNode, attribute, context))
                {
                    var paramTypeName = parameterNode.ToString();
                    if (!string.Equals(paramTypeName, "IDurableClient") && !string.Equals(paramTypeName, "IDurableEntityClient") && !string.Equals(paramTypeName, "IDurableOrchestrationClient"))
                    {
                        var diagnostic = Diagnostic.Create(Rule, parameterNode.GetLocation(), paramTypeName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
