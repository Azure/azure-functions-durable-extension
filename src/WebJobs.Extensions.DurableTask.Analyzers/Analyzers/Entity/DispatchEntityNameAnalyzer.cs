// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeDispatchEntityName, SyntaxKind.SimpleMemberAccessExpression);
        }

        private void AnalyzeDispatchEntityName(SyntaxNodeAnalysisContext context)
        {
            var expression = context.Node as MemberAccessExpressionSyntax;
            if (expression != null && SyntaxNodeUtils.IsInsideFunction(expression))
            {
                var name = expression.Name;
                if (name.ToString().StartsWith("DispatchAsync"))
                {
                    if (SyntaxNodeUtils.TryGetTypeArgumentIdentifierNode(expression, out SyntaxNode identifierNode))
                    {
                        if (SyntaxNodeUtils.TryGetFunctionNameParameterNode(expression, out SyntaxNode functionNameNode))
                        {
                            var identifierName = identifierNode.ToString();
                            var functionName = functionNameNode.ToString().Trim('"');
                            if (!string.Equals(identifierName, functionName) &&
                                !string.Equals(identifierName, "nameof(" + functionName + ")"))
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
