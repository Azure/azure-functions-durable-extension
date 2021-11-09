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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimerCodeFixProvider)), Shared]
    public class TimerCodeFixProvider : CodeFixProvider
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
            if (!TryGetInvocationExpression(expression, out SyntaxNode invocationExpression))
            {
                return;
            }

            SemanticModel semanticModel = await context.Document.GetSemanticModelAsync();
            var durableVersion = SyntaxNodeUtils.GetDurableVersion(semanticModel);

            if (SyntaxNodeUtils.IsInsideOrchestrationTrigger(invocationExpression) && durableVersion.Equals(DurableVersion.V2))
            {
                if (CodeFixProviderUtils.TryGetDurableOrchestrationContextVariableName(invocationExpression, out string variableName))
                {
                    var newExpression = "";
                    if (TryGetMillisecondsParameter(invocationExpression, out string milliseconds))
                    {

                        if (TryGetCancellationTokenParameter(invocationExpression, semanticModel, out string cancellationToken))
                        {
                            newExpression = variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.AddMilliseconds(" + milliseconds + "), " + cancellationToken + ")";
                        }
                        else
                        {
                            newExpression = variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.AddMilliseconds(" + milliseconds + "), CancellationToken.None)";
                        }
                    }
                    else if (TryGetTimespanParameter(invocationExpression, semanticModel, out string timeSpan))
                    {
                        if (TryGetCancellationTokenParameter(invocationExpression, semanticModel, out string cancellationToken))
                        {
                            newExpression = variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.Add(" + timeSpan + "), " + cancellationToken + ")";
                        }
                        else
                        {
                            newExpression = variableName + ".CreateTimer(" + variableName + ".CurrentUtcDateTime.Add(" + timeSpan + "), CancellationToken.None)";
                        }
                    }

                    context.RegisterCodeFix(
                    CodeAction.Create(FixTimerInOrchestrator.ToString(), c => ReplaceWithAwaitExpressionAsync(context.Document, expression, c, newExpression), nameof(TimerCodeFixProvider)),
                    diagnostic);
                }
            }
        }

        protected async Task<Document> ReplaceWithAwaitExpressionAsync(Document document, SyntaxNode expression, CancellationToken cancellationToken, string expressionString)
        {
            var newAwaitExpression = SyntaxFactory.AwaitExpression(
                SyntaxFactory.Token(SyntaxKind.AwaitKeyword),
                SyntaxFactory.ParseExpression(expressionString))
                    .WithLeadingTrivia(expression.GetLeadingTrivia())
                    .WithTrailingTrivia(expression.GetTrailingTrivia());

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(expression, newAwaitExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        private bool TryGetInvocationExpression(SyntaxNode expression, out SyntaxNode invocationExpression)
        {
            if (expression.IsKind(SyntaxKind.InvocationExpression))
            {
                invocationExpression = expression;
                return true;
            }

            invocationExpression = expression.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.InvocationExpression));
            return invocationExpression != null;
        }

        private bool TryGetTimespanParameter(SyntaxNode expression, SemanticModel semanticModel, out string timeSpan)
        {
            if (TryGetArgumentEnumerable(expression, out IEnumerable<SyntaxNode> argumentEnumerable))
            {
                foreach (SyntaxNode argument in argumentEnumerable)
                {
                    if (TryGetParameterOfType(argument, semanticModel, "System.TimeSpan", out string timeSpanReference))
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
                    if (TryGetParameterOfType(argument, semanticModel, "System.Threading.CancellationToken", out string cancellationTokenReference))
                    {
                        cancellationToken = cancellationTokenReference;
                        return true;
                    }
                }
            }

            cancellationToken = null;
            return false;
        }

        private bool TryGetParameterOfType(SyntaxNode argument, SemanticModel semanticModel, string typeToCompare, out string parameter)
        {
            if (TryGetIdentifierNameChildNodes(argument, out IEnumerable<SyntaxNode> node) ||
                TryGetSimpleMemberAccessExpressionChildNodes(argument, out node) ||
                TryGetObjectCreationExpressionChildNodes(argument, out node))
            {
                if (TryGetIdentifierNameChildNodes(node.FirstOrDefault(), out IEnumerable<SyntaxNode> identifiers))
                {
                    var typeWithoutNamespace = GetTypeWithoutNamespace(typeToCompare);
                    foreach (SyntaxNode identifier in identifiers)
                    {
                        if (string.Equals(identifier.ToString(), typeWithoutNamespace) ||
                            SyntaxNodeUtils.TryGetITypeSymbol(semanticModel, identifier, out ITypeSymbol type) &&
                            string.Equals(type.ToString(), typeToCompare))
                        {
                            parameter = node.First().ToString();
                            return true;
                        }
                    }
                }
            }

            parameter = null;
            return false;
        }

        private static bool TryGetObjectCreationExpressionChildNodes(SyntaxNode argument, out IEnumerable<SyntaxNode> nodes) =>
            TryGetChildNodes(argument, SyntaxKind.ObjectCreationExpression, out nodes);

        private static bool TryGetSimpleMemberAccessExpressionChildNodes(SyntaxNode argument, out IEnumerable<SyntaxNode> nodes) => 
            TryGetChildNodes(argument, SyntaxKind.SimpleMemberAccessExpression, out nodes);

        private static bool TryGetIdentifierNameChildNodes(SyntaxNode argument, out IEnumerable<SyntaxNode> nodes) =>
            TryGetChildNodes(argument, SyntaxKind.IdentifierName, out nodes);

        private static bool TryGetChildNodes(SyntaxNode argument, SyntaxKind kind, out IEnumerable<SyntaxNode> nodes)
        {
            if (argument == null)
            {
                nodes = null;
                return false;
            }

            if (argument.IsKind(kind))
            {
                nodes = new List<SyntaxNode>() { argument };
                return true;
            }
            
            nodes = argument.ChildNodes().Where(x => x.IsKind(kind));
            if (nodes.Any())
            {
                return true;
            }

            return false;
        }
        
        private static string GetTypeWithoutNamespace(string type)
        {
            var index = type.LastIndexOf('.') + 1;
            return type.Substring(index);
        }

        private bool TryGetMillisecondsParameter(SyntaxNode expression, out string milliseconds)
        {
            if (TryGetArgumentEnumerable(expression, out IEnumerable<SyntaxNode> argumentEnumerable))
            {
                foreach (SyntaxNode argument in argumentEnumerable)
                {
                    var numericLiteralNodeEnumerable = argument.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.NumericLiteralExpression));
                    if (numericLiteralNodeEnumerable != null)
                    {
                        milliseconds = numericLiteralNodeEnumerable.ToString();
                        return true;
                    }
                }
            }

            milliseconds = null;
            return false;
        }

        private bool TryGetArgumentEnumerable(SyntaxNode expression, out IEnumerable<SyntaxNode> arguments)
        {
            var argumentListEnumerable = expression.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.ArgumentList));
            if (argumentListEnumerable != null)
            {
                var argumentEnumerable = argumentListEnumerable.ChildNodes().Where(x => x.IsKind(SyntaxKind.Argument));
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
