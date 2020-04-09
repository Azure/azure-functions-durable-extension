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
    public class OrchestratorContextAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0201";

        private static readonly LocalizableString V1Title = new LocalizableResourceString(nameof(Resources.V1OrchestratorContextAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V2Title = new LocalizableResourceString(nameof(Resources.V2OrchestratorContextAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V1MessageFormat = new LocalizableResourceString(nameof(Resources.V1OrchestratorContextAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V1Description = new LocalizableResourceString(nameof(Resources.V1OrchestratorContextAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V2MessageFormat = new LocalizableResourceString(nameof(Resources.V2OrchestratorContextAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString V2Description = new LocalizableResourceString(nameof(Resources.V2OrchestratorContextAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Binding;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        private static readonly DiagnosticDescriptor V1Rule = new DiagnosticDescriptor(DiagnosticId, V1Title, V1MessageFormat, Category, Severity, isEnabledByDefault: true, description: V1Description);
        private static readonly DiagnosticDescriptor V2Rule = new DiagnosticDescriptor(DiagnosticId, V2Title, V2MessageFormat, Category, Severity, isEnabledByDefault: true, description: V2Description);
        
        private static DurableVersion version;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(V1Rule, V2Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSyntaxNodeAction(FindOrchestrationTriggers, SyntaxKind.Attribute);
        }

        public void FindOrchestrationTriggers(SyntaxNodeAnalysisContext context)
        {
            if (SyntaxNodeUtils.IsInsideFunction(context.SemanticModel, context.Node) && context.Node is AttributeSyntax attribute)
            {
                var semanticModel = context.SemanticModel;
                version = SyntaxNodeUtils.GetDurableVersion(semanticModel);

                if (string.Equals(attribute.ToString(), "OrchestrationTrigger"))
                {
                    if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(context, attribute, out SyntaxNode parameterNode))
                    {
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
                if (string.Equals(paramTypeName, "DurableOrchestrationContext") || string.Equals(paramTypeName, "DurableOrchestrationContextBase"))
                {
                    return true;
                }

            }
            else if (version.Equals(DurableVersion.V2))
            {
                if (string.Equals(paramTypeName, "IDurableOrchestrationContext"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
