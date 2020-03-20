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
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor CloseRule = new DiagnosticDescriptor(DiagnosticId, Title, CloseMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);
        public static readonly DiagnosticDescriptor MissingRule = new DiagnosticDescriptor(DiagnosticId, Title, MissingMessageFormat, Category, Severity, isEnabledByDefault: true, description: Description);


        private static string GetClosestString(string name, IEnumerable<string> availableNames)
        {
            return availableNames.OrderBy(x => x.LevenshteinDistance(name)).First();
        }

        public static void ReportProblems(
            CompilationAnalysisContext context, 
            IEnumerable<ActivityFunctionDefinition> availableFunctions, 
            IEnumerable<ActivityFunctionCall> calledFunctions)
        {
            foreach (var activityInvocation in calledFunctions)
            {
                if (!availableFunctions.Select(x => x.FunctionName).Contains(activityInvocation.Name))
                {
                    if (SyntaxNodeUtils.TryGetClosestString(activityInvocation.Name, availableFunctions.Select(x => x.FunctionName), out string closestName))
                    {
                        var diagnostic = Diagnostic.Create(CloseRule, activityInvocation.NameNode.GetLocation(), activityInvocation.Name, closestName);

                        context.ReportDiagnostic(diagnostic);
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(MissingRule, activityInvocation.NameNode.GetLocation(), activityInvocation.Name);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
