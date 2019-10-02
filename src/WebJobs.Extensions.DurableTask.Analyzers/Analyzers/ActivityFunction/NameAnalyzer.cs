// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class NameAnalyzer
    {
        public const string DiagnosticId = "DF0109";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString CloseMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerCloseMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MissingMessageFormat = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerMissingMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ActivityNameAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Activity;
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        public static DiagnosticDescriptor CloseRule = new DiagnosticDescriptor(DiagnosticId, Title, CloseMessageFormat, Category, severity, isEnabledByDefault: true, description: Description);
        public static DiagnosticDescriptor MissingRule = new DiagnosticDescriptor(DiagnosticId, Title, MissingMessageFormat, Category, severity, isEnabledByDefault: true, description: Description);


        private string GetClosestString(string name, IEnumerable<string> availableNames)
        {
            return availableNames.OrderBy(x => x.LevenshteinDistance(name)).First();
        }

        public void ReportProblems(CompilationAnalysisContext cac, IEnumerable<ActivityFunctionDefinition> availableFunctions, IEnumerable<ActivityFunctionCall> calledFunctions)
        {
            foreach (var node in calledFunctions)
            {
                if (!availableFunctions.Any())
                {
                    cac.ReportDiagnostic(Diagnostic.Create(MissingRule, node.NameNode.GetLocation(), node.Name));
                }
                else if (!availableFunctions.Select(x => x.FunctionName).Contains(node.Name))
                {
                    cac.ReportDiagnostic(Diagnostic.Create(CloseRule, node.NameNode.GetLocation(), node.Name, GetClosestString(node.Name, availableFunctions.Select(x => x.FunctionName))));
                }
            }
        }
    }
}
