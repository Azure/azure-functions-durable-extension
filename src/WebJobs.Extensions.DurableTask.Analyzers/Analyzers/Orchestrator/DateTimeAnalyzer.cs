﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DateTimeAnalyzer
    {
        public const string DiagnosticId = "DF0101";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DateTimeAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public static bool RegisterDiagnostic(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                var identifierName = descendant as IdentifierNameSyntax;
                if (identifierName != null)
                {
                    var identifierText = identifierName.Identifier.ValueText;
                    if (identifierText == "Now" || identifierText == "UtcNow" || identifierText == "Today")
                    {
                        var memberAccessExpression = identifierName.Parent;
                        var memberSymbol = semanticModel.GetSymbolInfo(memberAccessExpression).Symbol;

                        if (memberSymbol != null && memberSymbol.ToString().StartsWith("System.DateTime"))
                        {
                            var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                            context.ReportDiagnostic(diagnostic);

                            diagnosedIssue = true;
                        }
                    }
                }
            }

            return diagnosedIssue;
        }
    }
}
