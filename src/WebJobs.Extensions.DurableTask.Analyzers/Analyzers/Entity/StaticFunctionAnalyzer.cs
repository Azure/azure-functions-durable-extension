// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StaticFunctionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0306";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
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
                if (SyntaxNodeUtils.TryGetMethodDeclaration(attribute, out SyntaxNode methodDeclaration))
                {
                    var staticKeyword = methodDeclaration.ChildTokens().Where(x => x.IsKind(SyntaxKind.StaticKeyword));
                    if (!staticKeyword.Any())
                    {
                        var methodName = methodDeclaration.ChildTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken)).First();
                        var diagnostic = Diagnostic.Create(Rule, methodName.GetLocation(), methodName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
