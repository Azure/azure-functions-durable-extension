// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace DurableFunctionsAnalyzer.analyzers
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
        private const string Category = "OrchestrationContextAnalyzer";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Error;

        private static DiagnosticDescriptor V1Rule = new DiagnosticDescriptor(DiagnosticId, V1Title, V1MessageFormat, Category, severity, isEnabledByDefault: true, description: V1Description);
        private static DiagnosticDescriptor V2Rule = new DiagnosticDescriptor(DiagnosticId, V2Title, V2MessageFormat, Category, severity, isEnabledByDefault: true, description: V2Description);
        
        private DurableVersion version;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(V1Rule, V2Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(FindOrchestrationTriggers, SyntaxKind.Attribute);
        }

        public void FindOrchestrationTriggers(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;

            var semanticModel = context.SemanticModel;
            version = SyntaxNodeUtils.GetDurableVersion(semanticModel);

            if (string.Equals(attribute.ToString(), "OrchestrationTrigger"))
            {
                if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(out SyntaxNode parameterNode, attribute, context))
                {
                    if (!parameterTypeIsCorrectDurableType(parameterNode))
                    {
                        var rule = GetRuleFromVersion();
                        if (rule != null)
                        {
                            var diagnostic = Diagnostic.Create(rule, parameterNode.GetLocation(), parameterNode);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private DiagnosticDescriptor GetRuleFromVersion()
        {
            if (version.Equals(DurableVersion.V1))
            {
                return V1Rule;
            }
            else if (version.Equals(DurableVersion.V2))
            {
                return V2Rule;
            }
            return null;
        }

        private bool parameterTypeIsCorrectDurableType(SyntaxNode parameterNode)
        {
            var paramTypeName = parameterNode.ToString();

            if (version.Equals(DurableVersion.V1))
            {
                if (paramTypeName == "DurableOrchestrationContext" || paramTypeName == "DurableOrchestrationContextBase")
                {
                    return true;
                }

            }
            else if (version.Equals(DurableVersion.V2))
            {
                if (paramTypeName == "IDurableOrchestrationContext")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
