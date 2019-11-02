// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DateTimeCodeFixProvider)), Shared]
    public class DateTimeCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private static readonly LocalizableString FixDateTimeInOrchestrator = new LocalizableResourceString(nameof(Resources.FixDateTimeInOrchestrator), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixDeterministicAttribute = new LocalizableResourceString(nameof(Resources.FixDeterministicAttribute), Resources.ResourceManager, typeof(Resources));

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DateTimeAnalyzer.DiagnosticId); }
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

            var expression = root.FindNode(diagnosticSpan);

            if (SyntaxNodeUtils.IsInsideOrchestrator(expression))
            {
                if (TryGetDurableOrchestrationContextVariableName(expression, out string variableName))
                {
                    context.RegisterCodeFix(
                    CodeAction.Create(FixDateTimeInOrchestrator.ToString(), c => ReplaceWithIdentifierAsync(context.Document, expression, c, variableName + ".CurrentUtcDateTime"), nameof(DateTimeCodeFixProvider)), diagnostic);
                }
            }
            else if (SyntaxNodeUtils.IsMarkedDeterministic(expression))
            {
                context.RegisterCodeFix(
                CodeAction.Create(FixDeterministicAttribute.ToString(), c => RemoveDeterministicAttributeAsync(context.Document, expression, c), nameof(DateTimeCodeFixProvider)), diagnostic);
            }
        }
    }
}
