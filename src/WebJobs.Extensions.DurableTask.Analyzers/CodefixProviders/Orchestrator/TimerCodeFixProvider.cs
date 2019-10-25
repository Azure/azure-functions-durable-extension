// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimerCodeFixProvider)), Shared]
    public class TimerCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        private static readonly LocalizableString FixTimerInOrchestrator = new LocalizableResourceString(nameof(Resources.FixTimerInOrchestrator), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString FixDeterministicAttribute = new LocalizableResourceString(nameof(Resources.FixDeterministicAttribute), Resources.ResourceManager, typeof(Resources));

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

            SemanticModel semanticModel = await context.Document.GetSemanticModelAsync();
            var durableVersion = SyntaxNodeUtils.GetDurableVersion(semanticModel);

            if (SyntaxNodeUtils.IsInsideOrchestrator(expression) && durableVersion.Equals(DurableVersion.V2))
            {
                if (TryGetDurableOrchestrationContextVariableName(expression, out string variableName))
                {
                    var newExpression = "";
                    if (TryGetMillisecondsParameter(expression, out string milliseconds))
                    {

                        if (TryGetCancellationTokenParameter(expression, semanticModel, out string cancellationToken))
                        {
                            newExpression = "await " + variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.AddMilliseconds(" + milliseconds + "), " + cancellationToken + ")";
                        }
                        else
                        {
                            newExpression = "await " + variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.AddMilliseconds(" + milliseconds + "), CancellationToken.None)";
                        }
                    }
                    else if (TryGetTimespanParameter(expression, semanticModel, out string timeSpan))
                    {
                        if (TryGetCancellationTokenParameter(expression, semanticModel, out string cancellationToken))
                        {
                            newExpression = "await " + variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.Add(" + timeSpan + "), " + cancellationToken + ")";
                        }
                        else
                        {
                            newExpression = "await " + variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.Add(" + timeSpan + "), CancellationToken.None)";
                        }
                    }

                    context.RegisterCodeFix(
                    CodeAction.Create(FixTimerInOrchestrator.ToString(), c => ReplaceWithIdentifierAsync(context.Document, expression, c, newExpression), nameof(TimerCodeFixProvider)),
                    diagnostic);
                }
            }
            else if (SyntaxNodeUtils.IsMarkedDeterministic(expression))
            {
                context.RegisterCodeFix(
                CodeAction.Create(FixDeterministicAttribute.ToString(), c => RemoveDeterministicAttributeAsync(context.Document, expression, c), nameof(TimerCodeFixProvider)), diagnostic);
            }
        }

        private bool TryGetTimespanParameter(SyntaxNode expression, SemanticModel semanticModel,  out string timeSpan)
        {
            if (TryGetArgumentEnumerable(expression, out IEnumerable<SyntaxNode> argumentEnumerable))
            {
                foreach (SyntaxNode argument in argumentEnumerable)
                {
                    if (TryGetParameterOfType(argument, semanticModel, "TimeSpan", out string timeSpanReference))
                    {
                        timeSpan = timeSpanReference;
                        return true;
                    }
                }
            }

            timeSpan = null;
            return false;
        }

        private bool TryGetCancellationTokenParameter(SyntaxNode expression, SemanticModel semanticModel, out string cancellationToken)
        {
            if (TryGetArgumentEnumerable(expression, out IEnumerable<SyntaxNode> argumentEnumerable))
            {
                foreach (SyntaxNode argument in argumentEnumerable)
                {
                    if (TryGetParameterOfType(argument, semanticModel, "CancellationToken", out string cancellationTokenReference))
                    {
                        cancellationToken = cancellationTokenReference;
                        return true;
                    }
                }
            }

            cancellationToken = null;
            return false;
        }

        private bool TryGetParameterOfType(SyntaxNode argument, SemanticModel semanticModel, string typeToCompare, out string typeReference)
        {
            var objectCreationExpressionEnumerable = argument.ChildNodes().Where(x => x.IsKind(SyntaxKind.ObjectCreationExpression));
            if (objectCreationExpressionEnumerable.Any())
            {
                var identifierNameEnumerable = objectCreationExpressionEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                if (identifierNameEnumerable.Any())
                {
                    var identifierName = identifierNameEnumerable.First();
                    var typeInfo = semanticModel.GetTypeInfo(identifierName);
                    if (typeInfo.Type != null)
                    {
                        var type = typeInfo.Type.ToString();
                        if (string.Equals(type, typeToCompare))
                        {
                            typeReference = objectCreationExpressionEnumerable.First().ToString();
                            return true;
                        }
                    }
                }
            }
            else
            {
                var identifierNameEnumerable = argument.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName));
                if (identifierNameEnumerable.Any())
                {
                    var identifierName = identifierNameEnumerable.First();
                    var typeInfo = semanticModel.GetTypeInfo(identifierName);
                    if (typeInfo.Type != null)
                    {
                        var type = typeInfo.Type.ToString();
                        if (string.Equals(type, typeToCompare))
                        {
                            typeReference = identifierNameEnumerable.First().ToString();
                            return true;
                        }
                    }
                }
            }

            typeReference = null;
            return false;
        }

        private bool TryGetMillisecondsParameter(SyntaxNode expression, out string milliseconds)
        {
            if (TryGetArgumentEnumerable(expression, out IEnumerable<SyntaxNode> argumentEnumerable))
            {
                foreach (SyntaxNode argument in argumentEnumerable)
                {
                    var numericLiteralNodeEnumerable = argument.ChildNodes().Where(x => x.IsKind(SyntaxKind.NumericLiteralExpression));
                    if (numericLiteralNodeEnumerable.Any())
                    {
                        milliseconds = numericLiteralNodeEnumerable.First().ToString();
                        return true;
                    }
                }
            }

            milliseconds = null;
            return false;
        }

        private bool TryGetArgumentEnumerable(SyntaxNode expression, out IEnumerable<SyntaxNode> arguments)
        {
            var argumentListEnumerable = expression.ChildNodes().Where(x => x.IsKind(SyntaxKind.ArgumentList));
            if (argumentListEnumerable.Any())
            {
                var argumentEnumerable = argumentListEnumerable.First().ChildNodes().Where(x => x.IsKind(SyntaxKind.Argument));
                if (argumentEnumerable.Any())
                {
                    arguments = argumentEnumerable;
                    return true;
                }
            }

            arguments = null;
            return false;
        }
    }
}
