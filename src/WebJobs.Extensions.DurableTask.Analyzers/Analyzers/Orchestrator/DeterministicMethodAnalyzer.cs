﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DeterministicMethodAnalyzer : DiagnosticAnalyzer
    {
        private OrchestratorMethodCollector orchestratorMethodCollector;

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
                    CancellationTokenAnalyzer.Rule,
                    MethodInvocationAnalyzer.Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            orchestratorMethodCollector = new OrchestratorMethodCollector();

            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(orchestratorMethodCollector.FindOrchestratorMethods, SyntaxKind.MethodDeclaration);

                compilation.RegisterCompilationEndAction(RegisterAnalyzers);
            });
        }

        private void RegisterAnalyzers(CompilationAnalysisContext context)
        {
            var orchestratorMethods = orchestratorMethodCollector.GetOrchestratorMethods();
            Parallel.ForEach(orchestratorMethods, methodInformation =>
            {
                var semanticModel = methodInformation.SemanticModel;
                var methodDeclaration = methodInformation.Declaration;

                // | is the non short circuit or; this is important so that each method analyzes the code and reports all needed diagnostics.
                if (DateTimeAnalyzer.RegisterDiagnostic(context, semanticModel, methodDeclaration)
                    | EnvironmentVariableAnalyzer.RegisterDiagnostic(context, semanticModel, methodDeclaration)
                    | GuidAnalyzer.RegisterDiagnostic(context, semanticModel, methodDeclaration)
                    | IOTypesAnalyzer.RegisterDiagnostic(context, semanticModel, methodDeclaration)
                    | ThreadTaskAnalyzer.RegisterDiagnostic(context, semanticModel, methodDeclaration)
                    | TimerAnalyzer.RegisterDiagnostic(context, semanticModel, methodDeclaration)
                    | CancellationTokenAnalyzer.RegisterDiagnostic(context, methodDeclaration))
                {
                    MethodInvocationAnalyzer.RegisterDiagnostics(context, methodInformation);
                }
            });
        }
    }
}