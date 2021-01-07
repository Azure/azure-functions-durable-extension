// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class OrchestratorMethodCollector
    {
        private Dictionary<ISymbol, MethodInformation> orchestratorMethodDeclarations = new Dictionary<ISymbol, MethodInformation>();

        public void FindOrchestratorMethods(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var symbol = context.ContainingSymbol;
            if (context.Node is MethodDeclarationSyntax declaration
                && SyntaxNodeUtils.IsInsideOrchestratorFunction(semanticModel, declaration)
                && symbol != null)
            {
                var methodInformation = new MethodInformation()
                {
                    SemanticModel = semanticModel,
                    Declaration = declaration,
                    DeclarationSymbol = symbol,
                };

                if (!this.orchestratorMethodDeclarations.ContainsKey(symbol))
                {
                    this.orchestratorMethodDeclarations.Add(symbol, methodInformation);

                    this.FindInvokedMethods(semanticModel, methodInformation);
                }
            }
        }

        private void FindInvokedMethods(SemanticModel semanticModel, MethodInformation methodInformation)
        {
            var parentDeclaration = methodInformation.Declaration;
            var invocationExpressions = parentDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocationExpressions)
            {
                if (SyntaxNodeUtils.TryGetDeclaredSyntaxNode(semanticModel, invocation, out SyntaxNode methodDeclaration)
                    && methodDeclaration is MethodDeclarationSyntax
                    && SyntaxNodeUtils.TryGetISymbol(semanticModel, invocation, out ISymbol invokedSymbol))
                {
                    var invokedMethodInformation = new MethodInformation()
                    {
                        SemanticModel = semanticModel,
                        Declaration = methodDeclaration,
                        DeclarationSymbol = invokedSymbol,
                        Invocations = new List<InvocationExpressionSyntax>(){ invocation },
                        Parents = new HashSet<MethodInformation>(new List<MethodInformation>() { methodInformation }),
                    };

                    if (this.orchestratorMethodDeclarations.TryGetValue(invokedSymbol, out MethodInformation existingMethodInformation))
                    {
                        existingMethodInformation.Invocations.Add(invocation);
                        if (!existingMethodInformation.DeclarationSymbol.Equals(invokedSymbol))
                        {
                            existingMethodInformation.Parents.Add(methodInformation);
                        }
                    }
                    else
                    {
                        this.orchestratorMethodDeclarations.Add(invokedSymbol, invokedMethodInformation);

                        FindInvokedMethods(semanticModel, invokedMethodInformation);
                    }
                }
            }
        }

        public IEnumerable<MethodInformation> GetOrchestratorMethods()
        {
            return orchestratorMethodDeclarations.Values.ToList();
        }
    }
}