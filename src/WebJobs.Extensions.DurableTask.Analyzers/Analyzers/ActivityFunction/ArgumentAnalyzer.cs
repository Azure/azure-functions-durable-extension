// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ArgumentAnalyzer
    {
        public const string DiagnosticId = "DF0108";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MismatchMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString InputNotUsedMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormatNotUsed), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString InvalidNullMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormatInvalidNull), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor MismatchRule = new DiagnosticDescriptor(DiagnosticId, Title, MismatchMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor InputNotUsedRule = new DiagnosticDescriptor(DiagnosticId, Title, InputNotUsedMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor InvalidNullRule = new DiagnosticDescriptor(DiagnosticId, Title, InvalidNullMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static void ReportProblems(
            CompilationAnalysisContext context,
            IEnumerable<ActivityFunctionDefinition> functionDefinitions,
            IEnumerable<ActivityFunctionCall> functionInvocations)
        {
            foreach (var invocation in functionInvocations)
            {
                var definition = functionDefinitions.FirstOrDefault(x => x.FunctionName == invocation.FunctionName);
                if (definition != null && invocation.InputNode != null)
                {
                    if (invocation.InputNode.IsKind(SyntaxKind.NullLiteralExpression))
                    {
                        if (DefinitionInputIsNonNullableValueType(definition))
                        {
                            var diagnostic = Diagnostic.Create(InvalidNullRule, invocation.InputNode.GetLocation(), invocation.FunctionName, definition.InputType.ToString());

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                    else
                    {
                        if (DefinitionInputIsNotUsed(definition))
                        {
                            var diagnostic = Diagnostic.Create(InputNotUsedRule, invocation.InputNode.GetLocation(), invocation.FunctionName);

                            context.ReportDiagnostic(diagnostic);
                        }
                        else if (!IsValidArgumentForDefinition(invocation, definition))
                        {
                            var diagnostic = Diagnostic.Create(MismatchRule, invocation.InputNode.GetLocation(), invocation.FunctionName, definition.InputType, invocation.InputType);

                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private static bool DefinitionInputIsNonNullableValueType(ActivityFunctionDefinition definition)
        {
            var inputType = definition.InputType;
            return inputType != null && inputType.IsValueType && !inputType.Name.Equals("Nullable");
        }

        private static bool DefinitionInputIsNotUsed(ActivityFunctionDefinition definition)
        {
            return definition.InputType == null;
        }

        private static bool IsValidArgumentForDefinition(ActivityFunctionCall invocation, ActivityFunctionDefinition definition)
        {
            return SyntaxNodeUtils.IsMatchingDerivedOrCompatibleType(invocation.InputType, definition.InputType);
        }
    }
}


