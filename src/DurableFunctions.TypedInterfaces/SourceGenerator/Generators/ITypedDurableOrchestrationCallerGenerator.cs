// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public class ITypedDurableOrchestrationCallerGenerator : TypedCallerInterfaceGenerator
    {
        private static readonly string[] requiredUsings = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
            "System.Threading.Tasks"
        };

        private ITypedDurableOrchestrationCallerGenerator(List<DurableFunction> functions) : base(functions)
        {
        }

        public static bool TryGenerate(List<DurableFunction> functions, out CompilationUnitSyntax compilationSyntax)
        {
            var generator = new ITypedDurableOrchestrationCallerGenerator(functions);

            compilationSyntax = generator.Generate();
            return true;
        }

        private CompilationUnitSyntax Generate()
        {
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword, SyntaxKind.PartialKeyword);

            var requiredNamespaces = new HashSet<string>(requiredUsings);
            var memberList = new List<MemberDeclarationSyntax>();

            foreach (var function in functions)
            {
                if (function.Kind != DurableFunctionKind.Orchestration)
                    continue;

                memberList.Add(GenerateCallMethodWithRetry(function));
                memberList.Add(GenerateCallMethodWithoutRetry(function));
                memberList.Add(GenerateStartMethod(function));

                requiredNamespaces.UnionWith(function.RequiredNamespaces);
            }

            var members = SyntaxFactory.List(memberList);

            var @interface = SyntaxFactory.InterfaceDeclaration(Names.ITypedDurableOrchestrationCaller)
                .WithModifiers(modifiers)
                .WithMembers(members);

            var @namespace = GenerateNamespace().AddMembers(@interface);

            var usings = AsUsings(requiredNamespaces);

            return SyntaxFactory.CompilationUnit().AddUsings(usings).AddMembers(@namespace).NormalizeWhitespace();
        }
    }
}
