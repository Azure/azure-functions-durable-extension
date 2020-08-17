// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodAnalyzer
    {
        public const string DiagnosticId = "DF0107";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.MethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.MethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static bool RegisterDiagnostics(SyntaxNode methodDeclaration, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var methodInformationList = AnalyzeMethod(methodDeclaration, context, semanticModel);
            methodInformationList = RegisterAnalyzers(methodInformationList, context, semanticModel);
            return DiagnoseInvocations(methodInformationList, context, semanticModel);
        }

        private static List<MethodInformation> RegisterAnalyzers(List<MethodInformation> methodInformationList, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            foreach (MethodInformation method in methodInformationList)
            {
                var declaration = method.Declaration;

                // | is the non short circuit or; this is important so that each method analyzes the code and reports all needed diagnostics.
                if (DateTimeAnalyzer.RegisterDiagnostic(declaration, context, semanticModel) |
                    EnvironmentVariableAnalyzer.RegisterDiagnostic(declaration, context, semanticModel) |
                    GuidAnalyzer.RegisterDiagnostic(declaration, context, semanticModel) |
                    IOTypesAnalyzer.RegisterDiagnostic(declaration, context, semanticModel) |
                    ThreadTaskAnalyzer.RegisterDiagnostic(declaration, context, semanticModel) |
                    TimerAnalyzer.RegisterDiagnostic(declaration, context, semanticModel) |
                    RegisterDiagnostics(declaration, context, semanticModel))
                {
                    method.IsDeterministic = false;
                }
            }

            return methodInformationList;
        }

        private static bool DiagnoseInvocations(List<MethodInformation> methodInformationList, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (MethodInformation method in methodInformationList)
            {
                if (!method.IsDeterministic)
                {
                    var invocation = method.Invocation;

                    var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), invocation);

                    context.ReportDiagnostic(diagnostic);

                    diagnosedIssue = true;
                }
            }

            return diagnosedIssue;
        }

        private static List<MethodInformation> AnalyzeMethod(SyntaxNode methodDeclaration, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var methodInformationList = new List<MethodInformation>();

            foreach (SyntaxNode descendant in methodDeclaration.DescendantNodes())
            {
                if (descendant is InvocationExpressionSyntax invocation)
                {
                    if (SyntaxNodeUtils.TryGetISymbol(semanticModel, invocation, out ISymbol symbol) && symbol is IMethodSymbol methodSymbol)
                    {
                        var syntaxReference = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                        if (syntaxReference != null)
                        {
                            var declaration = syntaxReference.GetSyntax(context.CancellationToken);
                            if (declaration != null && !methodDeclaration.Equals(declaration))
                            {
                                methodInformationList.Add(new MethodInformation(declaration, invocation));
                            }
                        }
                    }
                }
            }

            return methodInformationList;
        }

        private class MethodInformation
        {

            public MethodInformation(SyntaxNode declaration, InvocationExpressionSyntax invocation)
            {
                this.Declaration = declaration;
                this.Invocation = invocation;
                this.IsDeterministic = true;
            }

            public bool IsDeterministic { get; set; }

            public SyntaxNode Declaration { get; }

            public InvocationExpressionSyntax Invocation { get; }
        }
    }
}
