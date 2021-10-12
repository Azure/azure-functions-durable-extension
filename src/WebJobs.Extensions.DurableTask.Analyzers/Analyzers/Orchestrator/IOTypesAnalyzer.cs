﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IOTypesAnalyzer
    {
        public const string DiagnosticId = "DF0105";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.IOTypesAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        internal static bool RegisterDiagnostic(CompilationAnalysisContext context, SemanticModel semanticModel, SyntaxNode method)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    try
                    {
                        if (SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, identifierName, out ITypeSymbol type))
                        {
                            if (IsIOClass(type.ToString()))
                            {
                                var diagnostic = Diagnostic.Create(Rule, identifierName.Identifier.GetLocation(), type);

                                context.ReportDiagnostic(diagnostic);

                                diagnosedIssue = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        var diagnostic = Diagnostic.Create(
                            ExceptionDiagnostic.Rule,
                            identifierName.Identifier.GetLocation(),
                            nameof(IOTypesAnalyzer),
                            semanticModel.Compilation.AssemblyName,
                            semanticModel.SyntaxTree.FilePath,
                            $"IdentifierNameSyntax node '{identifierName.Identifier}'",
                            e.ToString());

                        context.ReportDiagnostic(diagnostic);

                        return false;
                    }
                }
            }

            return diagnosedIssue;
        }

        private static bool IsIOClass(string s)
        {
            return s.Contains("HttpClient") || s.Contains("SqlConnection") || s.Contains("CloudBlobClient") || s.Contains("CloudQueueClient") || s.Contains("CloudTableClient") || s.Contains("DocumentClient") || s.Contains("WebRequest");
        }
    }
}
