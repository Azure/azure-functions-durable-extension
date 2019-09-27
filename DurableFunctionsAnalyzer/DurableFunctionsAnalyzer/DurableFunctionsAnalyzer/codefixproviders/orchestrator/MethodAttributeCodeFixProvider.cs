// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace WebJobs.Extensions.DurableTask.Analyzers
{
    //[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MethodAttributeCodeFixProvider)), Shared]
    public class MethodAttributeCodeFixProvider : DurableFunctionsCodeFixProvider
    {
        //Apply the [IsDeterministic] attribute?
        public override ImmutableArray<string> FixableDiagnosticIds => throw new NotImplementedException();

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            throw new NotImplementedException();
        }
    }
}
