// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ActivityTriggerParameterAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0115";

        // The activity trigger binding reserves this name for the raw JSON representation of the input.
        // See ActivityTriggerAttributeBindingProvider.GetBindingDataContract.
        private const string ReservedParameterName = "data";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityTriggerParameterAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ActivityTriggerParameterAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityTriggerParameterAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(FindReservedActivityParameterName, SyntaxKind.Attribute);
        }

        public void FindReservedActivityParameterName(SyntaxNodeAnalysisContext context)
        {
            if (SyntaxNodeUtils.IsInsideFunction(context.SemanticModel, context.Node)
                && context.Node is AttributeSyntax attribute
                && SyntaxNodeUtils.IsActivityTriggerAttribute(attribute)
                && attribute.Parent?.Parent is ParameterSyntax parameter
                && string.Equals(parameter.Identifier.ValueText, ReservedParameterName, StringComparison.OrdinalIgnoreCase))
            {
                var diagnostic = Diagnostic.Create(Rule, parameter.Identifier.GetLocation(), parameter.Identifier.ValueText);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
