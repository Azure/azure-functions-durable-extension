﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class GuidAnalyzer
    {
        public const string DiagnosticId = "DF0102";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.GuidAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        internal static bool RegisterDiagnostic(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                var identifierName = descendant as IdentifierNameSyntax;
                if (identifierName != null)
                {
                    if (identifierName.Identifier.ValueText == "NewGuid")
                    {
                        var memberAccessExpression = identifierName.Parent;
                        var invocationExpression = memberAccessExpression.Parent;
                        var memberSymbol = semanticModel.GetSymbolInfo(memberAccessExpression).Symbol;

                        if (memberSymbol != null && memberSymbol.ToString().StartsWith("System.Guid"))
                        {
                            var diagnostic = Diagnostic.Create(Rule, invocationExpression.GetLocation(), memberAccessExpression);

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
