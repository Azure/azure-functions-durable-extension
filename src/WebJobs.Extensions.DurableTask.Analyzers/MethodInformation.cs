// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class MethodInformation
    {
        private IList<InvocationExpressionSyntax> invocations;

        private HashSet<MethodInformation> parents;

        public SemanticModel SemanticModel { get; set; }

        public SyntaxNode Declaration { get; set; }

        public ISymbol DeclarationSymbol { get; set; }

        // Lazily initialized so that a MethodInformation instance does not allocate these collections up front
        // (and so callers that assign via object initializers do not trigger redundant allocations). Keeping the
        // collections non-null also prevents NullReferenceExceptions when they are mutated during method collection.
        public IList<InvocationExpressionSyntax> Invocations
        {
            get => this.invocations ?? (this.invocations = new List<InvocationExpressionSyntax>());
            set => this.invocations = value;
        }

        public HashSet<MethodInformation> Parents
        {
            get => this.parents ?? (this.parents = new HashSet<MethodInformation>());
            set => this.parents = value;
        }

        public override bool Equals(object obj)
        {
            return obj is MethodInformation information &&
                   EqualityComparer<ISymbol>.Default.Equals(DeclarationSymbol, information.DeclarationSymbol);
        }

        public override int GetHashCode()
        {
            return -1876124485 + EqualityComparer<ISymbol>.Default.GetHashCode(DeclarationSymbol);
        }
    }
}

