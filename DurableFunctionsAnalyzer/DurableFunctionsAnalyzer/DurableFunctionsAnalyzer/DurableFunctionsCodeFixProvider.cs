// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer
{
    public abstract class DurableFunctionsCodeFixProvider: CodeFixProvider
    {
        protected async Task<Document> RemoveDeterministicAttributeAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var attribute = SyntaxNodeUtils.GetDeterministicAttribute(identifierNode);
            var newRoot = root.RemoveNode(attribute, SyntaxRemoveOptions.KeepExteriorTrivia);
            return document.WithSyntaxRoot(newRoot);
        }

        protected async Task<Document> ReplaceWithIdentifierAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken, String expression)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(identifierNode, SyntaxFactory.IdentifierName(expression));
            return document.WithSyntaxRoot(newRoot);
        }

        protected async Task<Document> ReplaceWithExpressionAsync(Document document, SyntaxNode oldExpression, CancellationToken cancellationToken, String newExpression)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(oldExpression, SyntaxFactory.ParseExpression(newExpression, 0, null, false));
            return document.WithSyntaxRoot(newRoot);
        }


        protected static string GetDurableOrchestrationContextVariableName(SyntaxNode node)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(out SyntaxNode methodDeclaration, node))
            {
                var parameterList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ParameterList)).First();

                foreach (SyntaxNode parameter in parameterList.ChildNodes())
                {
                    var attributeList = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                    if (attributeList.Count() >= 1 && attributeList.First().ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                    {

                        return parameter.ChildTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken)).First().ToString();
                    }
                }
            }
            return null;
        }
    }
}
