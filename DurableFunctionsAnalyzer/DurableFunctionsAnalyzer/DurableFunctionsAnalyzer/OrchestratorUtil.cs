using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace DurableFunctionsAnalyzer
{
    public static class OrchestratorUtil
    {
        /// <summary>
        /// Checks to see if a SyntaxNode is inside an orchestrator function
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static bool IsInsideOrchestrator(SyntaxNode node)
        {
            var methodDeclaration = GetMethodDeclaration(node);
            if (methodDeclaration == null)
            {
                return false;
            }

            var parameterList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ParameterList)).First();

            foreach (SyntaxNode parameter in parameterList.ChildNodes())
            {
                var attributeList = parameter.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
                if (attributeList.Count() >= 1 && attributeList.First().ChildNodes().First().ToString().Equals("OrchestrationTrigger"))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Returns the Method Declaration SyntaxNode that the input SyntaxNode is within. Returns null if thereis no Method Declaration
        /// surrounding the input node.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static SyntaxNode GetMethodDeclaration(SyntaxNode node)
        {
            var currNode = node.IsKind(SyntaxKind.MethodDeclaration) ? node : node.Parent;
            while (!currNode.IsKind(SyntaxKind.MethodDeclaration))
            {
                if (currNode.IsKind(SyntaxKind.CompilationUnit))
                {
                    return null;
                }
                currNode = currNode.Parent;
            }
            return currNode;
        }

        /// <summary>
        /// Checks to see if a SyntaxNode is in a method with the IsDeterministic attribute
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static bool IsMarkedDeterministic(SyntaxNode node)
        {
            if (GetIsDeterministicAttribute(node) != null)
            {
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static SyntaxNode GetIsDeterministicAttribute(SyntaxNode node)
        {
            var methodDeclaration = GetMethodDeclaration(node);
            if (methodDeclaration == null)
            {
                return null;
            }

            var IEnumeratorAttributeList = methodDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.AttributeList));
            if (!IEnumeratorAttributeList.Any())
            {
                return null;
            }

            foreach (SyntaxNode attributeList in IEnumeratorAttributeList)
            {
                if (attributeList.ToString().Equals("[IsDeterministic]"))
                {

                    return attributeList;
                }
            }
            return null;
        }
    }
}
