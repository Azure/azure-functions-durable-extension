﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BindingAnalyzer
    {
        public const string DiagnosticId = "DF0112";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.BindingAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.BindingAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private const string Category = SupportedCategories.Orchestrator;
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, Severity, isEnabledByDefault: true);

        public static bool RegisterDiagnostic(CompilationAnalysisContext context, SyntaxNode method)
        {
            var diagnosedIssue = false;

            var parameterList = method.ChildNodes().First(x => x.IsKind(SyntaxKind.ParameterList));

            foreach (SyntaxNode descendant in parameterList.DescendantNodes())
            {
                if (descendant is AttributeSyntax attribute)
                {
                    var attributeName = attribute.Name.ToString();
                    if (attributeName != "OrchestrationTrigger" && !IsExcludedAttributeName(attributeName))
                    {
                        var diagnostic = Diagnostic.Create(Rule, attribute.GetLocation(), attributeName);

                        if (context.Compilation.ContainsSyntaxTree(method.SyntaxTree))
                        {
                            context.ReportDiagnostic(diagnostic);
                        }

                        diagnosedIssue = true;
                    }
                }
            }
            
            return diagnosedIssue;
        }

        private static bool IsExcludedAttributeName(string attributeName)
        {
            var excludedAttributeNames = new List<string> { "AllowNull", "DisallowNull", "MaybeNull", "NotNull", "MaybeNullWhen", 
                "NotNullWhen", "NotNullIfNotNull", "MemberNotNull", "MemberNotNullWhen", "DoesNotReturn", "DoesNotReturnIf" };
            return excludedAttributeNames.Contains(attributeName);
        }
    }
}
