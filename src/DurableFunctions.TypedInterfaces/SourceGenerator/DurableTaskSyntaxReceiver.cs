// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;

namespace DurableFunctions.TypedInterfaces
{
    /// <summary>
    /// Syntax receiver for accumulating DurableFunctions during the compilation process.
    /// </summary>
    public class DurableTaskSyntaxReceiver : ISyntaxContextReceiver
    {
        public bool Searched { get; private set; } = false;
        public bool Initialized { get; private set; } = false;

        public INamedTypeSymbol DurableOrchestrationContextTypeSymbol { get; set; } = null;
        public INamedTypeSymbol DurableClientTypeSymbol { get; set; } = null;

        public List<DurableFunction> DurableFunctions { get; set; } = new List<DurableFunction>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            try
            {
                // Only search on one visit, because we just need need to find the DurableTask types
                // once and then hold a reference of them to use later.
                if (!Searched)
                {
                    Searched = true;
                    Initialized = true;

                    Initialized &= DurableFunctionUtility.TryFindDurableOrchestrationContextType(context.SemanticModel, out var durableOrchestrationContextTypeSymbol);
                    Initialized &= DurableFunctionUtility.TryFindDurableClientType(context.SemanticModel, out var durableClientTypeSymbol);

                    DurableOrchestrationContextTypeSymbol = durableOrchestrationContextTypeSymbol;
                    DurableClientTypeSymbol = durableClientTypeSymbol;
                }

                // Must have found DurableTask types in order to find DurableFunctions
                if (!Initialized)
                    return;

                // Looking for things that are methods
                if (!(context.Node is MethodDeclarationSyntax method))
                    return;

                if (!DurableFunction.TryParse(context.SemanticModel, method, out DurableFunction function))
                    return;

                Debug.WriteLine($"Adding function '{function.Name}'");

                DurableFunctions.Add(function);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }

}
