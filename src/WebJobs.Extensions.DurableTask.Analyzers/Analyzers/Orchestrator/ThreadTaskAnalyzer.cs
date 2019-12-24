// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ThreadTaskAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0104";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ThreadTaskAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierTask, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierTaskFactory, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierThread, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierTaskContinueWith, SyntaxKind.IdentifierName);
        }

        private static void AnalyzeIdentifierTask(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                var identifierText = identifierName.Identifier.ValueText;
                if (identifierText == "Run" || identifierText == "Factory.StartNew")
                {
                    var memberAccessExpression = identifierName.Parent;
                    var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol;

                    if (memberSymbol == null || !memberSymbol.ToString().StartsWith("System.Threading.Tasks.Task"))
                    {
                        return;
                    }

                    if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }

                    var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeIdentifierTaskFactory(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                var identifierText = identifierName.Identifier.ValueText;
                if (identifierText == "StartNew")
                {
                    var memberAccessExpression = identifierName.Parent;
                    var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol;

                    if (memberSymbol == null || !memberSymbol.ToString().StartsWith("System.Threading.Tasks.TaskFactory"))
                    {
                        return;
                    }

                    if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }

                    var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeIdentifierThread(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                var identifierText = identifierName.Identifier.ValueText;
                if (identifierText == "Start")
                {
                    var memberAccessExpression = identifierName.Parent;
                    var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol;

                    if (memberSymbol == null || !memberSymbol.ToString().StartsWith("System.Threading.Thread"))
                    {
                        return;
                    }

                    if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }

                    var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeIdentifierTaskContinueWith(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                var identifierText = identifierName.Identifier.ValueText;
                if (identifierText == "ContinueWith")
                {
                    var memberAccessExpression = identifierName.Parent;
                    var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol;

                    if (memberSymbol == null || !memberSymbol.ToString().StartsWith("System.Threading.Tasks.Task"))
                    {
                        return;
                    }

                    if (HasExecuteSynchronously(identifierName))
                    {
                        return;
                    }

                    if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }

                    var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool HasExecuteSynchronously(SyntaxNode node)
        {
            if(!SyntaxNodeUtils.TryGetInvocationExpression(node, out SyntaxNode invocationExpression))
            {
                return false;
            }

            var argumentList = invocationExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.ArgumentList)).FirstOrDefault();

            if (argumentList != null)
            {
                foreach (SyntaxNode argument in argumentList.ChildNodes())
                {
                    var simpleMemberAccessExpression = argument.ChildNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression)).FirstOrDefault();

                    if (simpleMemberAccessExpression != null)
                    {
                        var identifierNames = simpleMemberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));

                        foreach (SyntaxNode identifier in identifierNames)
                        {
                            if (identifier.ToString().Equals("ExecuteSynchronously"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
