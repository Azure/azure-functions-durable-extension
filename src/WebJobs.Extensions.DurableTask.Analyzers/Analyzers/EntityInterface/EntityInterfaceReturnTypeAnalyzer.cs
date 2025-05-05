// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class EntityInterfaceReturnTypeAnalyzer
    {
        public const string DiagnosticId = "DF0304";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityInterfaceReturnTypeAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityInterfaceReturnTypeAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityInterfaceReturnTypeAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.EntityInterface;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description, 
            customTags: WellKnownDiagnosticTags.CompilationEnd);


        public static void ReportProblems(CompilationAnalysisContext context, EntityInterface entityInterface)
        {
            var childNodes = entityInterface.InterfaceDeclaration.ChildNodes();
            foreach (var node in childNodes)
            {
                if (SyntaxNodeUtils.TryGetMethodReturnTypeNode(node, out SyntaxNode returnTypeNode))
                {
                    var returnType = returnTypeNode.ToString();
                    if (!returnType.Equals("void") && !returnType.StartsWith("Task"))
                    {
                        var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), returnType);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
