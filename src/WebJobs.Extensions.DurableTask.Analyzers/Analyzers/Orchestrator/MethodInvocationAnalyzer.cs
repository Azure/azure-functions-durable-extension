// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodInvocationAnalyzer
    {
        public const string DiagnosticId = "DF0107";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.MethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.MethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static void RegisterDiagnostics(CompilationAnalysisContext context, MethodInformation methodInformation)
        {
            var methodInvocations = methodInformation?.Invocations;
            if (methodInvocations != null && methodInvocations.Any())
            {
                foreach(InvocationExpressionSyntax invocation in methodInvocations)
                {
                    var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), invocation);

                    context.ReportDiagnostic(diagnostic);
                }

                RegisterDiagnosticsOnParents(context, methodInformation);
            }
        }

        private static void RegisterDiagnosticsOnParents(CompilationAnalysisContext context, MethodInformation methodInformation)
        {
            var parents = methodInformation.Parents;
            if (parents != null && parents.Any())
            {
                foreach (MethodInformation parent in parents)
                {
                    RegisterDiagnostics(context, parent);
                }
            }
        }
    }
}
