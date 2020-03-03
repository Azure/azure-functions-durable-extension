// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0203";

        private static readonly LocalizableString V1Title = new LocalizableResourceString(nameof(Resources.V1ClientAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V1MessageFormat = new LocalizableResourceString(nameof(Resources.V1ClientAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V1Description = new LocalizableResourceString(nameof(Resources.V1ClientAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V2Title = new LocalizableResourceString(nameof(Resources.V2ClientAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V2MessageFormat = new LocalizableResourceString(nameof(Resources.V2ClientAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V2Description = new LocalizableResourceString(nameof(Resources.V2ClientAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Binding;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private static readonly DiagnosticDescriptor V1Rule = new DiagnosticDescriptor(DiagnosticId, V1Title, V1MessageFormat, Category, Severity, isEnabledByDefault: true, description: V1Description);
        private static readonly DiagnosticDescriptor V2Rule = new DiagnosticDescriptor(DiagnosticId, V2Title, V2MessageFormat, Category, Severity, isEnabledByDefault: true, description: V2Description);

        private static DurableVersion version;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(V1Rule, V2Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSyntaxNodeAction(FindEntityTriggers, SyntaxKind.Attribute);
        }

        public void FindEntityTriggers(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;

            var semanticModel = context.SemanticModel;
            version = SyntaxNodeUtils.GetDurableVersion(semanticModel);

            if (AttributeMatchesVersionClientBinding(attribute))
            {
                if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(context, attribute, out SyntaxNode parameterNode))
                {
                    var paramTypeName = parameterNode.ToString();
                    if (!ParameterTypeIsCorrectDurableType(parameterNode))
                    {
                        if (TryGetRuleFromVersion(out DiagnosticDescriptor rule))
                        {
                            var diagnostic = Diagnostic.Create(rule, parameterNode.GetLocation(), parameterNode);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private bool AttributeMatchesVersionClientBinding(AttributeSyntax attribute)
        {
            if (attribute != null)
            {
                var attributeString = attribute.ToString();

                if (version == DurableVersion.V1 && string.Equals(attributeString, "OrchestrationClient"))
                {
                    return true;
                }

                if (version == DurableVersion.V2 && string.Equals(attributeString, "DurableClient"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetRuleFromVersion(out DiagnosticDescriptor rule)
        {
            if (version.Equals(DurableVersion.V1))
            {
                rule = V1Rule;
                return true;
            }
            else if (version.Equals(DurableVersion.V2))
            {
                rule = V2Rule;
                return true;
            }

            rule = null;
            return false;
        }

        private bool ParameterTypeIsCorrectDurableType(SyntaxNode parameterNode)
        {
            var paramTypeName = parameterNode.ToString();

            if (version.Equals(DurableVersion.V1))
            {
                if (string.Equals(paramTypeName, "DurableOrchestrationClient") || string.Equals(paramTypeName, "DurableOrchestrationClientBase"))
                {
                    return true;
                }

            }
            else if (version.Equals(DurableVersion.V2))
            {
                if (string.Equals(paramTypeName, "IDurableClient") || string.Equals(paramTypeName, "IDurableEntityClient") || string.Equals(paramTypeName, "IDurableOrchestrationClient"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
