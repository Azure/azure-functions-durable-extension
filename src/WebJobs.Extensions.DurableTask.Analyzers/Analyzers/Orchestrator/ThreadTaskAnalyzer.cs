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

                    if (!memberSymbol?.ToString().StartsWith("System.Threading.Tasks.Task") ?? true)
                    {
                        return;
                    }
                    else if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                        context.ReportDiagnostic(diagnostic);
                    }
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

                    if (!memberSymbol?.ToString().StartsWith("System.Threading.Thread") ?? true)
                    {
                        return;
                    }
                    else if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                        context.ReportDiagnostic(diagnostic);
                    }
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

                    if (!memberSymbol?.ToString().StartsWith("System.Threading.Tasks.Task") ?? true)
                    {
                        return;
                    }
                    else if (HasExecuteSynchronously(identifierName))
                    {
                        return;
                    }
                    else if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private bool HasExecuteSynchronously(SyntaxNode node)
        {
            var invocationExpression = GetInvocationExpression(node);
            if (invocationExpression == null)
            {
                return false;
            }

            var argumentList = invocationExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.ArgumentList)).First();

            foreach (SyntaxNode argument in argumentList.ChildNodes())
            {
                var simpleMemberAccessExpression = argument.ChildNodes().Where(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression)).First();
                var identifierNameList = simpleMemberAccessExpression.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                foreach (SyntaxNode identifierName in identifierNameList)
                {
                    if (identifierName.ToString().Equals("ExecuteSynchronously"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private SyntaxNode GetInvocationExpression(SyntaxNode node)
        {
            var currNode = node.IsKind(SyntaxKind.InvocationExpression) ? node : node.Parent;
            while (!currNode.IsKind(SyntaxKind.InvocationExpression))
            {
                if (currNode.IsKind(SyntaxKind.CompilationUnit))
                {
                    return null;
                }
                currNode = currNode.Parent;
            }
            return currNode;
        }
    }
}
