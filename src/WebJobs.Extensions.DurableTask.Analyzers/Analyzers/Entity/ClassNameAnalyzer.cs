// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClassNameAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0305";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityClassNameAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString CloseMessageFormat = new LocalizableResourceString(nameof(Resources.EntityClassNameAnalyzerCloseMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MissingMessageFormat = new LocalizableResourceString(nameof(Resources.EntityClassNameAnalyzerMissingMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityClassNameAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Entity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private List<string> classNames = new List<string>();
        private List<SyntaxNode> entityTriggerAttributes = new List<SyntaxNode>();

        public static readonly DiagnosticDescriptor ClassNameCloseRule = new DiagnosticDescriptor(DiagnosticId, Title, CloseMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description, 
            customTags: WellKnownDiagnosticTags.CompilationEnd);
        public static readonly DiagnosticDescriptor ClassNameMissingRule = new DiagnosticDescriptor(DiagnosticId, Title, MissingMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description, 
            customTags: WellKnownDiagnosticTags.CompilationEnd);
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(ClassNameCloseRule, ClassNameMissingRule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            ClassNameAnalyzer classNameAnalyzer = new ClassNameAnalyzer();
            context.RegisterCompilationStartAction(compilation =>
            {
                compilation.RegisterSyntaxNodeAction(classNameAnalyzer.FindClassDeclaration, SyntaxKind.ClassDeclaration);
                compilation.RegisterSyntaxNodeAction(classNameAnalyzer.FindEntityTrigger, SyntaxKind.Attribute);

                compilation.RegisterCompilationEndAction(classNameAnalyzer.ReportDiagnostics);
            });
        }

        private void ReportDiagnostics(CompilationAnalysisContext context)
        {
            foreach (AttributeSyntax entityTrigger in entityTriggerAttributes)
            {
                // GetSemanticModel is expensive - if examining this code, refactor following guidance from RS1030
#pragma warning disable RS1030
                SemanticModel semanticModel = context.Compilation.GetSemanticModel(entityTrigger.SyntaxTree);
#pragma warning restore RS1030
                if (SyntaxNodeUtils.TryGetFunctionNameAndNode(semanticModel, entityTrigger, out SyntaxNode attributeArgument, out string functionName))
                {
                    if (!classNames.Contains(functionName))
                    {
                        if (SyntaxNodeUtils.TryGetClosestString(functionName, classNames, out string closestName))
                        {
                            var diagnostic = Diagnostic.Create(ClassNameCloseRule, attributeArgument.GetLocation(), functionName, closestName);

                            context.ReportDiagnostic(diagnostic);
                        }
                        else
                        {
                            var diagnostic = Diagnostic.Create(ClassNameMissingRule, attributeArgument.GetLocation(), functionName);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private void FindClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ClassDeclarationSyntax classDeclaration)
            {
                var className = classDeclaration.Identifier.ToString();
                classNames.Add(className);
            }
        }

        private void FindEntityTrigger(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;
            if (SyntaxNodeUtils.IsEntityTriggerAttribute(attribute))
            {
                entityTriggerAttributes.Add(attribute);
            }
        }
    }
}
