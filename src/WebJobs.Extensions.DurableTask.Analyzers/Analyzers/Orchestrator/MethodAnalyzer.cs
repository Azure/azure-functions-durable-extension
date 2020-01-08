using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

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
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

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
                var methodDeclaration = (MethodDeclarationSyntax)method.Declaration;
                
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
                var invocation = descendant as InvocationExpressionSyntax;
                if (invocation != null)
                {
                    var methodSymbol = semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
                    if (methodSymbol != null)
                    {
                        var syntaxReference = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                        if (syntaxReference != null)
                        {
                            var declaration = syntaxReference.GetSyntax(context.CancellationToken);
                            if (declaration != null)
                            {
                                methodInformationList.Add(new MethodInformation(declaration, invocation));
                            }
                        }
                    }
                }
            }

            return methodInformationList;
        }
    }
}
