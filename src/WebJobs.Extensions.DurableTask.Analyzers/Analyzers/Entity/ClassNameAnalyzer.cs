// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClassNameAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0305";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityClassNameAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityClassNameAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityClassNameAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Entity;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeAttributeClassName, SyntaxKind.Attribute);
        }

        private static void AnalyzeAttributeClassName(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;
            if (SyntaxNodeUtils.IsEntityTriggerAttribute(attribute))
            {
                if (SyntaxNodeUtils.TryGetFunctionNameParameterNode(attribute, out SyntaxNode attributeArgument))
                {
                    var functionName = attributeArgument.ToString().Trim('"');
                    if (SyntaxNodeUtils.TryGetClassSymbol(attribute, context.SemanticModel, out INamedTypeSymbol classSymbol))
                    {
                        var className = classSymbol.Name.ToString();

                        if (!ClassNameMatchesFunctionName(classSymbol, functionName))
                        {
                            var diagnosticClassName = Diagnostic.Create(Rule, classSymbol.Locations[0], className, functionName);
                            var diagnosticAttribute = Diagnostic.Create(Rule, attributeArgument.GetLocation(), className, functionName);

                            context.ReportDiagnostic(diagnosticClassName);
                            context.ReportDiagnostic(diagnosticAttribute);
                        }
                    }
                }
            }
        }

        private static bool ClassNameMatchesFunctionName(INamedTypeSymbol classSymbol, string functionName)
        {
            var classNameWithNamespce = classSymbol.ToString();
            var className = classSymbol.Name.ToString();

            var nameOfNamespace = "nameof(" + classNameWithNamespce + ")";
            var nameOfClassName = "nameof(" + className + ")";

            if (String.Equals(className, functionName) || String.Equals(nameOfClassName, functionName) || String.Equals(nameOfNamespace, functionName))
            {
                return true;
            }

            return false;
        }
    }
}
