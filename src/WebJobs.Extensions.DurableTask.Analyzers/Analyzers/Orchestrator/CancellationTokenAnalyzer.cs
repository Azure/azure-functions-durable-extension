// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CancellationTokenAnalyzer
    {
        public const string DiagnosticId = "DF0111";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.CancellationTokenAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.CancellationTokenAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true);

        public static bool RegisterDiagnostic(CompilationAnalysisContext context, SyntaxNode method)
        {
            var diagnosedIssue = false;
            
            if (SyntaxNodeUtils.IsInsideOrchestrationTrigger(method))
            {
                foreach (SyntaxNode descendant in method.DescendantNodes())
                {
                    if (descendant is ParameterSyntax parameter)
                    {
                        var identifierType = parameter.Type;
                        if (identifierType != null && identifierType.ToString() == "CancellationToken")
                        {
                            var diagnostic = Diagnostic.Create(Rule, parameter.GetLocation());

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