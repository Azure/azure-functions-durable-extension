// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ThreadTaskAnalyzer
    {
        public const string DiagnosticId = "DF0104";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ThreadTaskAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        internal static bool RegisterDiagnostic(CompilationAnalysisContext context, SemanticModel semanticModel, SyntaxNode method)
        {
            // | is the non short circuit or; this is important so that each method analyzes the code and reports all needed diagnostics.
            return (AnalyzeIdentifierTask(method, context, semanticModel) |
                AnalyzeIdentifierTaskFactory(method, context, semanticModel) |
                AnalyzeIdentifierTaskContinueWith(method, context, semanticModel) |
                AnalyzeIdentifierThread(method, context, semanticModel));
        }

        private static bool AnalyzeIdentifierTask(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    var identifierText = identifierName.Identifier.ValueText;
                    if (identifierText == "Run" || identifierText == "Factory.StartNew")
                    {
                        var memberAccessExpression = identifierName.Parent;
                        if (SyntaxNodeUtils.TryGetISymbol(semanticModel, memberAccessExpression, out ISymbol memberSymbol))
                        {
                            if (memberSymbol.ToString().StartsWith("System.Threading.Tasks.Task"))
                            {
                                var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                                context.ReportDiagnostic(diagnostic);

                                diagnosedIssue = true;
                            }
                        }
                    }
                }
            }

            return diagnosedIssue;
        }

        private static bool AnalyzeIdentifierTaskFactory(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    var identifierText = identifierName.Identifier.ValueText;
                    if (identifierText == "StartNew")
                    {
                        var memberAccessExpression = identifierName.Parent;
                        if (SyntaxNodeUtils.TryGetISymbol(semanticModel, memberAccessExpression, out ISymbol memberSymbol))
                        {
                            if (memberSymbol.ToString().StartsWith("System.Threading.Tasks.TaskFactory"))
                            {
                                var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), memberAccessExpression);

                                context.ReportDiagnostic(diagnostic);

                                diagnosedIssue = true;
                            }
                        }
                    }
                }
            }

            return diagnosedIssue;
        }

        private static bool AnalyzeIdentifierThread(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    var identifierText = identifierName.Identifier.ValueText;
                    if (identifierText == "Start")
                    {
                        var memberAccessExpression = identifierName.Parent;
                        if (SyntaxNodeUtils.TryGetISymbol(semanticModel, memberAccessExpression, out ISymbol memberSymbol))
                        {
                            if (memberSymbol != null && memberSymbol.ToString().StartsWith("System.Threading.Thread"))
                            {
                                var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), "Thread.Start");

                                context.ReportDiagnostic(diagnostic);

                                diagnosedIssue = true;
                            }
                        }
                    }
                }
            }

            return diagnosedIssue;
        }

        private static bool AnalyzeIdentifierTaskContinueWith(SyntaxNode method, CompilationAnalysisContext context, SemanticModel semanticModel)
        {
            var diagnosedIssue = false;

            foreach (SyntaxNode descendant in method.DescendantNodes())
            {
                if (descendant is IdentifierNameSyntax identifierName)
                {
                    var identifierText = identifierName.Identifier.ValueText;
                    if (identifierText == "ContinueWith")
                    {
                        if (!HasExecuteSynchronously(identifierName))
                        {
                            var memberAccessExpression = identifierName.Parent;

                            var diagnostic = Diagnostic.Create(Rule, memberAccessExpression.GetLocation(), "Task.ContinueWith");

                            context.ReportDiagnostic(diagnostic);
                            
                            diagnosedIssue = true;
                        }
                    }
                }
            }

            return diagnosedIssue;
        }

        private static bool HasExecuteSynchronously(SyntaxNode node)
        {
            if(!SyntaxNodeUtils.TryGetInvocationExpression(node, out SyntaxNode invocationExpression))
            {
                return false;
            }

            var argumentList = invocationExpression.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.ArgumentList));

            if (argumentList != null)
            {
                foreach (SyntaxNode argument in argumentList.ChildNodes())
                {
                    var simpleMemberAccessExpression = argument.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.SimpleMemberAccessExpression));

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
