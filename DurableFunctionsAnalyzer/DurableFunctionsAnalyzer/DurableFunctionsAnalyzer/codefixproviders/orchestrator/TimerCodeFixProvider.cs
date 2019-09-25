// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableFunctionsAnalyzer.analyzers;
using DurableFunctionsAnalyzer.analyzers.orchestrator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer.codefixproviders
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimerCodeFixProvider)), Shared]
    public class TimerCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private const string title = "Replace with context.CreateTimer";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(TimerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var expression = root.FindNode(diagnosticSpan, false, true);

            if (SyntaxNodeUtils.IsInsideOrchestrator(expression))
            {
                var contextName = GetDurableOrchestrationContextVariableName(expression);
                var previousParameter = GetPreviousParameterValue(expression)?.ToString();
                var newExpression = contextName + ".CreateTimer(" + contextName + ".CurrentUtcDateTime.AddMilliseconds(" + previousParameter + "))";

                context.RegisterCodeFix(
                CodeAction.Create("Replace with (IDurableOrchestrationContext).CreateTimer()", c => ReplaceWithIdentifierAsync(context.Document, expression, c, newExpression)),
                diagnostic);
            }
            else if (SyntaxNodeUtils.IsMarkedDeterministic(expression))
            {
                context.RegisterCodeFix(
                CodeAction.Create("Remove Deterministic Attribute", c => RemoveDeterministicAttributeAsync(context.Document, expression, c)), diagnostic);
            }
        }

        private SyntaxNode GetPreviousParameterValue(SyntaxNode expression)
        {
            var argumentListEnumerable = expression.ChildNodes().Where(x => x.IsKind(SyntaxKind.ArgumentList));
            if (argumentListEnumerable.Any())
            {
                var argumentEnumerable = argumentListEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.Argument));
                if (argumentEnumerable.Any())
                {
                    var argumentNode = argumentEnumerable.First().ChildNodes().First();
                    return argumentNode;
                }
            }
            return null;
        }
    }
}
