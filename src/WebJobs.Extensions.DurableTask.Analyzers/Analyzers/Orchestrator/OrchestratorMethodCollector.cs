// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    /// <summary>
    /// This class collects all orchestator methods and methods used within orchestrators defined in the solution.
    /// This is meant to be used with a DiagnosticAnalyzer by registering actions on SyntaxKind.MethodDeclaration.
    /// </summary>
    public class OrchestratorMethodCollector
    {
        private readonly Dictionary<ISymbol, MethodInformation> orchestratorMethodDeclarations = new Dictionary<ISymbol, MethodInformation>();

        /// <summary>
        /// Method used to collect orchestrator methods and methods used in orchestrators that are defined in
        /// the solution. Call this on a DiagnosticAnalyzer by registering actions on SyntaxKind.MethodDeclaration.
        /// </summary>
        /// <param name="context"></param>
        public void FindOrchestratorMethods(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var symbol = context.ContainingSymbol;
            if (symbol != null
                && context.Node is MethodDeclarationSyntax declaration
                && SyntaxNodeUtils.IsInsideOrchestratorFunction(semanticModel, declaration))
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

        private void FindInvokedMethods(SemanticModel semanticModel, MethodInformation parentMethodInformation)
        {
            var parentDeclaration = parentMethodInformation.Declaration;
            var invocationExpressions = parentDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocationExpressions)
            {
                if (SyntaxNodeUtils.TryGetDeclaredSyntaxNode(semanticModel, invocation, out SyntaxNode invokedMethodDeclaration)
                    && invokedMethodDeclaration is MethodDeclarationSyntax
                    && SyntaxNodeUtils.TryGetISymbol(semanticModel, invocation, out ISymbol invokedSymbol))
                {

                    if (this.orchestratorMethodDeclarations.TryGetValue(invokedSymbol, out MethodInformation existingMethodInformation))
                    {
                        existingMethodInformation.Invocations.Add(invocation);
                        if (!existingMethodInformation.Equals(parentMethodInformation))
                        {
                            existingMethodInformation.Parents.Add(parentMethodInformation);
                        }
                    }
                    else
                    {
                        var invokedMethodInformation = new MethodInformation()
                        {
                            SemanticModel = semanticModel,
                            Declaration = invokedMethodDeclaration,
                            DeclarationSymbol = invokedSymbol,
                            Invocations = new List<InvocationExpressionSyntax>() { invocation },
                            Parents = new HashSet<MethodInformation>(new List<MethodInformation>() { parentMethodInformation }),
                        };

                        this.orchestratorMethodDeclarations.Add(invokedSymbol, invokedMethodInformation);

                        FindInvokedMethods(semanticModel, invokedMethodInformation);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a collection of methods stored in this OrchestratorMethodCollector.
        /// Call this as a CompilationEndAction on the DiagnosticAnalyzer using an instance of this class.
        /// </summary>
        /// <returns>The collection of orchestrator methods and methods used within orchestrator functions.</returns>
        public IEnumerable<MethodInformation> GetOrchestratorMethods()
        {
            return orchestratorMethodDeclarations.Values;
        }
    }
}
