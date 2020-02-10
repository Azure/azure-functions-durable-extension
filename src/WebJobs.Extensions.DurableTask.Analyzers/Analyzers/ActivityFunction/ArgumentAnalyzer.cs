﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ArgumentAnalyzer
    {
        public const string DiagnosticId = "DF0108";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString InputNotUsedMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerMessageFormatNotUsed), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor InputNotUsedRule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);

        public static void ReportProblems(CompilationAnalysisContext context, IEnumerable<ActivityFunctionDefinition> availableFunctions, IEnumerable<ActivityFunctionCall> calledFunctions)
        {
            foreach (var node in calledFunctions)
            {
                if (availableFunctions.Where(x => x.FunctionName == node.Name).Any())
                {
                    var functionDefinition = availableFunctions.Where(x => x.FunctionName == node.Name).SingleOrDefault();
                    if (functionDefinition.InputType != node.ParameterType)
                    {
                        if (node.ParameterType.StartsWith("Microsoft.Azure.WebJobs"))
                        {
                            var notUsedDiagnostic = Diagnostic.Create(InputNotUsedRule, node.ParameterNode.GetLocation(), node.Name, functionDefinition.InputType, node.ParameterType);

                            context.ReportDiagnostic(notUsedDiagnostic);
                        }

                        var diagnostic = Diagnostic.Create(Rule, node.ParameterNode.GetLocation(), node.Name, functionDefinition.InputType, node.ParameterType);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}


