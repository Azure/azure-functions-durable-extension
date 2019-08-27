using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DurableFunctionsAnalyzer
{
    public abstract class OrchestratorCodeFixProvider: CodeFixProvider
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="document"></param>
        /// <param name="identifierNode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected async Task<Document> RemoveIsDeterministicAttributeAsync(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var attribute = OrchestratorUtil.GetIsDeterministicAttribute(identifierNode);
            var newRoot = root.RemoveNode(attribute, SyntaxRemoveOptions.KeepExteriorTrivia);
            return document.WithSyntaxRoot(newRoot);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="document"></param>
        /// <param name="identifierNode"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected async Task<Document> ReplaceWithExpression(Document document, SyntaxNode identifierNode, CancellationToken cancellationToken, String expression)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(identifierNode, SyntaxFactory.IdentifierName(expression));
            return document.WithSyntaxRoot(newRoot);
        }

        
        protected static String GetContextVariableName(SyntaxNode node)
        {
            var methodDeclaration = OrchestratorUtil.GetMethodDeclaration(node);
            if (methodDeclaration == null)
            {
                return null;
            }

            var parameterList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ParameterList)).First();

            foreach (SyntaxNode parameter in parameterList.ChildNodes())
            {
                var attributeList = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                if (attributeList.Count() >= 1 && attributeList.First().ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                {

                    return parameter.ChildTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken)).First().ToString();
                }
            }
            return null;
        }
    }
}
