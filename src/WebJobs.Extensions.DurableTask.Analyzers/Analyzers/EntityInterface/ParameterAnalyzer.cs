// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ParameterAnalyzer
    {
        public const string DiagnosticId = "DF0303";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityInterfaceParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityInterfaceParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityInterfaceParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.EntityInterface;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description, 
            customTags: WellKnownDiagnosticTags.CompilationEnd);


        public static void ReportProblems(CompilationAnalysisContext context, EntityInterface entityInterface)
        {
            var interfaceChildNodes = entityInterface.InterfaceDeclaration.ChildNodes();
            foreach (var node in interfaceChildNodes)
            {
                if (node.IsKind(SyntaxKind.MethodDeclaration))
                {
                    var parameterList = node.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.ParameterList));
                    if (parameterList == null)
                    {
                        return;
                    }

                    var parameters = parameterList.ChildNodes().Where(x => x.IsKind(SyntaxKind.Parameter));
                    if (parameters.Count() > 1)
                    {
                        var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
