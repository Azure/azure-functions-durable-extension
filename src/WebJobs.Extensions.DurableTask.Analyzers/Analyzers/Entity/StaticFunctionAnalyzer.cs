// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class StaticFunctionAnalyzer
    {
        public const string DiagnosticId = "DF0306";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Entity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static void ReportProblems(CompilationAnalysisContext context, SyntaxNode methodDeclaration)
        {
            var staticKeyword = methodDeclaration.ChildTokens().Where(x => x.IsKind(SyntaxKind.StaticKeyword)).FirstOrDefault();
            if (staticKeyword == null)
            {
                if (IsInEntityClass(methodDeclaration))
                {
                    var methodName = methodDeclaration.ChildTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken)).FirstOrDefault();

                    var diagnostic = Diagnostic.Create(Rule, methodName.GetLocation(), methodName);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsInEntityClass(SyntaxNode methodDeclaration)
        {
            if (SyntaxNodeUtils.TryGetFunctionNameAndNode(methodDeclaration, out SyntaxNode attributeArgument, out string functionName))
            {
                if (SyntaxNodeUtils.TryGetClassName(methodDeclaration, out string className))
                {
                    if (string.Equals(className, functionName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
