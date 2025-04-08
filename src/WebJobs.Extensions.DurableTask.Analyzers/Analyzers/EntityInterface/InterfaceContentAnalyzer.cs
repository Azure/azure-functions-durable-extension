// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class InterfaceContentAnalyzer
    {
        public const string DiagnosticId = "DF0302";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityInterfaceContentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString NotAMethodMessageFormat = new LocalizableResourceString(nameof(Resources.EntityInterfaceContentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString NoMethodsMessageFormat = new LocalizableResourceString(nameof(Resources.EntityInterfaceContentAnalyzerNoMethodsMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityInterfaceContentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.EntityInterface;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor NotAMethodRule = new DiagnosticDescriptor(DiagnosticId, Title, NotAMethodMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description, 
            customTags: WellKnownDiagnosticTags.CompilationEnd);
        public static readonly DiagnosticDescriptor NoMethodsRule = new DiagnosticDescriptor(DiagnosticId, Title, NoMethodsMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description, 
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static void ReportProblems(CompilationAnalysisContext context, EntityInterface entityInterface)
        {
            var interfaceDeclaration = entityInterface.InterfaceDeclaration;
            var interfaceChildNodes = interfaceDeclaration.ChildNodes();

            if (!interfaceChildNodes.Any())
            {
                var diagnostic = Diagnostic.Create(NoMethodsRule, interfaceDeclaration.GetLocation(), interfaceDeclaration);

                context.ReportDiagnostic(diagnostic);
                return;
            }

            foreach (var node in interfaceChildNodes)
            {
                // Only methods and implemented interfaces are allowed in an entity interface.
                if (!node.IsKind(SyntaxKind.MethodDeclaration) && !node.IsKind(SyntaxKind.BaseList))
                {
                    var diagnostic = Diagnostic.Create(NotAMethodRule, node.GetLocation(), node);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
