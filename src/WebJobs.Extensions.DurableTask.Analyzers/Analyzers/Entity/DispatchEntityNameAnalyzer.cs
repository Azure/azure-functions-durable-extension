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
    [DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
    public class DispatchEntityNameAnalyzer: DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0307";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DispatchEntityNameAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DispatchEntityNameAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DispatchEntityNameAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Entity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private List<SyntaxNode> methodDeclarations = new List<SyntaxNode>();

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics {
            get
            {
                return ImmutableArray.Create(
                    Rule,
                    StaticFunctionAnalyzer.Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            DispatchEntityNameAnalyzer dispatchAnalyzer = new DispatchEntityNameAnalyzer();
            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(dispatchAnalyzer.AnalyzeDispatchAndFindMethodDeclarations, SyntaxKind.SimpleMemberAccessExpression);

                compilation.RegisterCompilationEndAction(dispatchAnalyzer.RegisterStaticAnalyzer);
            });
        }

        private void RegisterStaticAnalyzer(CompilationAnalysisContext context)
        {
            foreach(SyntaxNode methodDeclaration in methodDeclarations)
            {
                StaticFunctionAnalyzer.ReportProblems(context, methodDeclaration);
            }
        }

        private void AnalyzeDispatchAndFindMethodDeclarations(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is MemberAccessExpressionSyntax expression &&
                SyntaxNodeUtils.IsInsideFunction(context.SemanticModel, expression))
            {
                var name = expression.Name;
                if (name.ToString().StartsWith("DispatchAsync"))
                {
                    if(SyntaxNodeUtils.TryGetMethodDeclaration(expression, out MethodDeclarationSyntax methodDeclaration))
                    {
                        methodDeclarations.Add(methodDeclaration);
                    }

                    if (SyntaxNodeUtils.TryGetTypeArgumentIdentifier(expression, out SyntaxNode identifierNode))
                    {
                        if (SyntaxNodeUtils.TryGetFunctionName(context.SemanticModel, expression, out string functionName))
                        {
                            var identifierName = identifierNode.ToString();
                            if (!string.Equals(identifierName, functionName))
                            {
                                var diagnostic = Diagnostic.Create(Rule, identifierNode.GetLocation(), identifierNode, functionName);

                                context.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                }
            }
        }
    }
}
