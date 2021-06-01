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
    public class TypedDurableActivityCallerGenerator : TypedCallerImplementationGenerator
    {
        private const string ContextFieldName = "_context";

        private static readonly string[] requiredUsings = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
            "System.Threading.Tasks"
        };

        private readonly List<DurableFunction> functions;

        private TypedDurableActivityCallerGenerator(List<DurableFunction> functions)
        {
            this.functions = functions;
        }

        public static bool TryGenerate(List<DurableFunction> functions, out CompilationUnitSyntax compilationSyntax)
        {
            var generator = new TypedDurableActivityCallerGenerator(functions);

            compilationSyntax = generator.Generate();
            return true;
        }

        private CompilationUnitSyntax Generate()
        {
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword, SyntaxKind.PartialKeyword);
            var baseTypes = AsBaseList(Names.ITypedDurableActivityCaller);

            var memberList = new List<MemberDeclarationSyntax>();

            memberList.Add(AsField(Names.IDurableOrchestrationContext, ContextFieldName));
            memberList.Add(GenerateConstructor());

            var requiredNamespaces = new HashSet<string>(requiredUsings);

            foreach (var function in functions)
            {
                if (function.Kind != DurableFunctionKind.Activity)
                    continue;

                memberList.Add(GenerateCallMethodWithRetry(function));
                memberList.Add(GenerateCallMethodWithoutRetry(function));

                requiredNamespaces.UnionWith(function.RequiredNamespaces);
            }

            var members = SyntaxFactory.List(memberList);

            var @class = SyntaxFactory.ClassDeclaration(Names.TypedDurableActivityCaller)
                .WithModifiers(modifiers)
                .WithBaseList(baseTypes)
                .WithMembers(members);

            var @namespace = GenerateNamespace().AddMembers(@class);

            var usings = AsUsings(requiredNamespaces);

            return SyntaxFactory.CompilationUnit().AddUsings(usings).AddMembers(@namespace).NormalizeWhitespace();
        }

        private ConstructorDeclarationSyntax GenerateConstructor()
        {
            const string contextParameterName = "context";

            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);
            var parameters = AsParameterList(AsParameter(Names.IDurableOrchestrationContext, contextParameterName));
            var body = SyntaxFactory.Block(AsSimpleAssignmentExpression(ContextFieldName, contextParameterName));

            return SyntaxFactory.ConstructorDeclaration(Names.TypedDurableActivityCaller)
                .WithModifiers(modifiers)
                .WithParameterList(parameters)
                .WithBody(body);
        }
    }
}
