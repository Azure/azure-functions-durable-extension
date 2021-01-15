// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class MethodInformation
    {
        public SemanticModel SemanticModel { get; set; }

        public SyntaxNode Declaration { get; set; }

        public ISymbol DeclarationSymbol { get; set; }

        public IList<InvocationExpressionSyntax> Invocations { get; set; }

        public HashSet<MethodInformation> Parents { get; set; }

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

