﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Analyzers
{
    public class ActivityFunctionCall
    {
        public string Name { get; set; }
        public SyntaxNode NameNode { get; set; }
        public SyntaxNode ParameterNode { get; set; }
        public string ParameterType { get; set; }
        public string ExpectedReturnType { get; set; }
        public SyntaxNode InvocationExpression { get; set; }
    }
}
