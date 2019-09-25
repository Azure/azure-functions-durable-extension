// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace DurableFunctionsAnalyzer.analyzers.entityInterface
{
    class ReturnTypeAnalyzer
    {
        public const string DiagnosticId = "DF0304";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityInterfaceReturnTypeAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityInterfaceReturnTypeAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityInterfaceReturnTypeAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Argument";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        public static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);


        public void ReportProblems(CompilationAnalysisContext context, EntityInterface entityInterface)
        {
            var childNodes = entityInterface.InterfaceDeclaration.ChildNodes();
            foreach (var node in childNodes)
            {
                if (node.IsKind(SyntaxKind.MethodDeclaration))
                {
                    var returnTypeNode = node.ChildNodes().Where(x => x.IsKind(SyntaxKind.PredefinedType) || x.IsKind(SyntaxKind.IdentifierName) || x.IsKind(SyntaxKind.GenericName));
                    if (returnTypeNode.Any())
                    {
                        var returnType = returnTypeNode.First().ToString();
                        if (returnType.Equals("void") || returnType.StartsWith("Task"))
                        {
                            return;
                        }

                        var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node, returnType);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
