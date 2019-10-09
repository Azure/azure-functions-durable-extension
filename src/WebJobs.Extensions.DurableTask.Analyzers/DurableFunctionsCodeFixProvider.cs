// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public abstract class DurableFunctionsCodeFixProvider : CodeFixProvider
    {
        protected async Task<Document> RemoveDeterministicAttributeAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (SyntaxNodeUtils.TryGetDeterministicAttribute(identifierNode, out SyntaxNode deterministicAttribute))
            {
                var newRoot = root.RemoveNode(deterministicAttribute, SyntaxRemoveOptions.KeepExteriorTrivia);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }

        protected async Task<Document> ReplaceWithIdentifierAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken, string expression)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(identifierNode, SyntaxFactory.IdentifierName(expression));
            return document.WithSyntaxRoot(newRoot);
        }

        protected async Task<Document> ReplaceWithExpressionAsync(Document document, SyntaxNode oldExpression, CancellationToken cancellationToken, string newExpression)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(oldExpression, SyntaxFactory.ParseExpression(newExpression, 0, null, false));
            return document.WithSyntaxRoot(newRoot);
        }

        public override FixAllProvider GetFixAllProvider()
        {
            // Disables fix-all support, according to the Roslyn analyzer analyzer
            return null;
        }

        protected static bool TryGetDurableOrchestrationContextVariableName(SyntaxNode node, out string variableName)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(node, out SyntaxNode methodDeclaration))
            {
                var parameterList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ParameterList)).FirstOrDefault();

                foreach (SyntaxNode parameter in parameterList.ChildNodes())
                {
                    var attributeListEnumerable = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                    foreach (SyntaxNode attribute in attributeListEnumerable)
                    {
                        if (attribute.ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                        {
                            var identifierName = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.IdentifierName)).FirstOrDefault()?.ToString();
                            if (string.Equals(identifierName, "IDurableOrchestrationContext") || string.Equals(identifierName, "DurableOrchestrationContext") || string.Equals(identifierName, "DurableOrchestrationContextBase"))
                            {
                                //A parameter will always have an IdentifierToken
                                var identifierToken = parameter.ChildTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken)).First().ToString();
                                if (!string.Equals(identifierToken, ""))
                                {
                                    variableName = identifierToken;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            variableName = null;
            return false;
        }
    }
}
