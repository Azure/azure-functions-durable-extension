// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OrchestratorAnalyzer : DiagnosticAnalyzer
    {
        private List<SyntaxNode> orchestratorMethodDeclarations = new List<SyntaxNode>();
        private SemanticModel semanticModel;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    DateTimeAnalyzer.Rule,
                    EnvironmentVariableAnalyzer.Rule,
                    GuidAnalyzer.Rule,
                    IOTypesAnalyzer.Rule,
                    ThreadTaskAnalyzer.Rule,
                    TimerAnalyzer.V1Rule,
                    TimerAnalyzer.V2Rule,
                    MethodAnalyzer.Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            OrchestratorAnalyzer orchestratorAnalyzer = new OrchestratorAnalyzer();
            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(orchestratorAnalyzer.AnalyzeMethod, SyntaxKind.MethodDeclaration);

                compilation.RegisterCompilationEndAction(orchestratorAnalyzer.RegisterAnalyzers);
            });
        }

        private void RegisterAnalyzers(CompilationAnalysisContext context)
        {
            foreach (SyntaxNode method in this.orchestratorMethodDeclarations)
            {
                DateTimeAnalyzer.RegisterDiagnostic(method, context, this.semanticModel);
                EnvironmentVariableAnalyzer.RegisterDiagnostic(method, context, this.semanticModel);
                GuidAnalyzer.RegisterDiagnostic(method, context, this.semanticModel);
                IOTypesAnalyzer.RegisterDiagnostic(method, context, this.semanticModel);
                ThreadTaskAnalyzer.RegisterDiagnostic(method, context, this.semanticModel);
                TimerAnalyzer.RegisterDiagnostic(method, context, this.semanticModel);
                MethodAnalyzer.RegisterDiagnostics(method, context, this.semanticModel);
            }
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is MethodDeclarationSyntax declaration
                && SyntaxNodeUtils.IsInsideOrchestrator(declaration)
                && SyntaxNodeUtils.IsInsideFunction(context.SemanticModel, declaration))
            {
                if (this.semanticModel == null)
                {
                    this.semanticModel = context.SemanticModel;
                }

                this.orchestratorMethodDeclarations.Add(declaration);
            }
        }
    }
}
