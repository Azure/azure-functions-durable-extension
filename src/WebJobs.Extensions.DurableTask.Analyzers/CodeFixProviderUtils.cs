// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public static class CodeFixProviderUtils
    {
        public static async Task<Document> ReplaceWithIdentifierAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken, string identifierString)
        {
            var newIdentifier = SyntaxFactory.IdentifierName(identifierString)
                .WithLeadingTrivia(identifierNode.GetLeadingTrivia())
                .WithTrailingTrivia(identifierNode.GetTrailingTrivia());

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(identifierNode, newIdentifier);
            return document.WithSyntaxRoot(newRoot);
        }

        public static bool TryGetDurableOrchestrationContextVariableName(SyntaxNode node, out string variableName)
        {
            if (SyntaxNodeUtils.TryGetMethodDeclaration(node, out MethodDeclarationSyntax methodDeclaration))
            {
                var parameterList = methodDeclaration.ParameterList;

                foreach (SyntaxNode parameter in parameterList.ChildNodes())
                {
                    var attributeLists = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                    foreach (SyntaxNode attributeList in attributeLists)
                    {
                        if (attributeList.ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
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
