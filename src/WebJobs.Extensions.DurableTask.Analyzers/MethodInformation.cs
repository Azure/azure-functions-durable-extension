using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    internal class MethodInformation
    {

        public MethodInformation(SyntaxNode declaration, InvocationExpressionSyntax invocation)
        {
            this.Declaration = declaration;
            this.Invocation = invocation;
            this.IsDeterministic = true;
        }

        public bool IsDeterministic { get; set;}

        public SyntaxNode Declaration { get; }

        public InvocationExpressionSyntax Invocation { get; }
    }
}