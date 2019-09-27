// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.CodeAnalysis;

namespace WebJobs.Extensions.DurableTask.Analyzers
{
    public class ActivityFunctionCall
    {
        public string Name { get; set; }
        public SyntaxNode NameNode { get; set; }
        public SyntaxNode ParameterNode { get; set; }
        public String ParameterType { get; set; }
        public string ExpectedReturnType { get; set; }
        public SyntaxNode ExpectedReturnTypeNode { get; set; }
    }
}
