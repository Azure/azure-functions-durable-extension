// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Models;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Utils;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Generators
{
    public class IGeneratedDurableOrchestrationCallerGenerator : GeneratedCallerInterfaceGenerator
    {
        private static readonly string[] requiredUsings = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
            "System.Threading.Tasks"
        };

        private IGeneratedDurableOrchestrationCallerGenerator(List<DurableFunction> functions) : base(functions)
        {
        }

        public static bool TryGenerate(List<DurableFunction> functions, out CompilationUnitSyntax compilationSyntax)
        {
            var generator = new IGeneratedDurableOrchestrationCallerGenerator(functions);

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

            var @interface = SyntaxFactory.InterfaceDeclaration(Names.IGeneratedDurableOrchestrationCaller)
                .WithModifiers(modifiers)
                .WithMembers(members);

            var @namespace = GenerateNamespace().AddMembers(@interface);

            var usings = AsUsings(requiredNamespaces);

            return SyntaxFactory.CompilationUnit().AddUsings(usings).AddMembers(@namespace).NormalizeWhitespace();
        }
    }
}
