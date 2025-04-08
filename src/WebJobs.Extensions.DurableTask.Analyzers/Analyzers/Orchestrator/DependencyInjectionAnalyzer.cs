// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class DependencyInjectionAnalyzer
    {
        public const string DiagnosticId = "DF0113";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DependencyInjectionAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static bool RegisterDiagnostic(CompilationAnalysisContext context, SyntaxNode method)
        {
            var diagnosedIssue = false;

            if (!SyntaxNodeUtils.IsInStaticClass(method))
            {
                if(TryGetInjectedVariables(method, out List<SyntaxNode> injectedVariables))
                {
                    var methodVariablesUsed = method.DescendantNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));

                    var varaiblesToDiagnose = methodVariablesUsed.Where(x => injectedVariables.Exists(y => y.ToString() == x.ToString()));

                    foreach (var variable in varaiblesToDiagnose)
                    {
                        var diagnostic = Diagnostic.Create(Rule, variable.GetLocation(), variable);

                        if (context.Compilation.ContainsSyntaxTree(method.SyntaxTree))
                        {
                            context.ReportDiagnostic(diagnostic);
                        }

                        diagnosedIssue = true;
                    }
                }
            }

            return diagnosedIssue;
        }

        private static bool TryGetInjectedVariables(SyntaxNode method, out List<SyntaxNode> injectedVariables)
        {
            injectedVariables = new List<SyntaxNode>();
            var addedVariable = false;
            if (SyntaxNodeUtils.TryGetConstructor(method, out ConstructorDeclarationSyntax constructor))
            {
                var parameters = constructor.ParameterList.ChildNodes();
                var injectedParameterNames = new List<SyntaxToken>();
                foreach (SyntaxNode parameter in parameters)
                {
                    injectedParameterNames.Add(parameter.ChildTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierToken)));
                }

                var assignementExpressions = constructor.DescendantNodes().Where(x => x.IsKind(SyntaxKind.SimpleAssignmentExpression));

                foreach (AssignmentExpressionSyntax assignmentExpression in assignementExpressions)
                {
                    var injectedRightSideNode = assignmentExpression.Right.DescendantNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName) && injectedParameterNames.Contains(((IdentifierNameSyntax)x).Identifier));

                    if (injectedRightSideNode == null)
                    {
                        continue;
                    }

                    var assignedLeftSideNode = assignmentExpression.Left.DescendantNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.IdentifierName));

                    if (assignedLeftSideNode != null)
                    {
                        injectedVariables.Add(assignedLeftSideNode);
                        addedVariable = true;
                    }
                }
            }

            return addedVariable;
        }
    }
}
