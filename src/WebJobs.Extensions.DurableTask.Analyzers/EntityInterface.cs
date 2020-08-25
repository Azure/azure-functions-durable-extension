// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class EntityInterface
    {
        public string Name { get; set; }

        public SyntaxNode InterfaceDeclaration { get; set; }
    }
}
