// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Models;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public class TypedDurableOrchestrationStarterGenerator : BaseGenerator
    {
        private const string ClientFieldName = "_client";

        private static readonly string[] requiredUsings = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
            "System.Threading.Tasks"
        };

        private List<DurableFunction> functions;

        private TypedDurableOrchestrationStarterGenerator(List<DurableFunction> functions)
        {
            this.functions = functions;
        }

        public static bool TryGenerate(List<DurableFunction> functions, out CompilationUnitSyntax compilationSyntax)
        {
            var generator = new TypedDurableOrchestrationStarterGenerator(functions);
            compilationSyntax = generator.Generate();
            return true;
        }

        private CompilationUnitSyntax Generate()
        {
            var modifiers = AsModifierList(SyntaxKind.PublicKeyword, SyntaxKind.PartialKeyword);
            var baseTypes = AsBaseList(Names.ITypedDurableOrchestrationStarter);

            var memberList = new List<MemberDeclarationSyntax>();

            memberList.Add(AsField(Names.IDurableClient, ClientFieldName));
            memberList.Add(GenerateConstructor());

            var requiredNamespaces = new HashSet<string>(requiredUsings);

            foreach (var function in functions)
            {
                if (function.Kind != DurableFunctionKind.Orchestration)
                    continue;

                memberList.Add(GenerateStartMethod(function));

                requiredNamespaces.UnionWith(function.RequiredNamespaces);
            }

            var members = SyntaxFactory.List(memberList);

            var @class = SyntaxFactory.ClassDeclaration(Names.TypedDurableOrchestrationStarter)
                .WithModifiers(modifiers)
                .WithBaseList(baseTypes)
                .WithMembers(members);

            var @namespace = GenerateNamespace().AddMembers(@class);
            var usings = AsUsings(requiredNamespaces);

            return SyntaxFactory.CompilationUnit().AddUsings(usings).AddMembers(@namespace).NormalizeWhitespace();
        }

        private ConstructorDeclarationSyntax GenerateConstructor()
        {
            const string clientParameterName = "client";

            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);
            var parameters = AsParameterList(AsParameter(Names.IDurableClient, clientParameterName));
            var body = SyntaxFactory.Block(AsSimpleAssignmentExpression(ClientFieldName, clientParameterName));

            return SyntaxFactory.ConstructorDeclaration(Names.TypedDurableOrchestrationStarter)
                .WithModifiers(modifiers)
                .WithParameterList(parameters)
                .WithBody(body);
        }

        protected MethodDeclarationSyntax GenerateStartMethod(
            DurableFunction function
        )
        {
            const string instanceParameterName = "instance";

            var parameters = function.Parameters;

            var methodName = $"Start{function.Name}";

            var modifiers = AsModifierList(SyntaxKind.PublicKeyword);

            var leadingTrivia = AsCrefSummary(function.FullTypeName);

            var parameterList = AsParameterList()
                .AddParameters(function.Parameters.Select(p => AsParameter(p.Type.ToString(), p.Name)).ToArray())
                .AddParameters(AsParameter("string", instanceParameterName).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))));

            // Add body
            var callMethodName = "StartNewAsync";
            var functionNameParameter = $"\"{function.Name}\"";
            var callContextParameters = (parameters.Count == 0) ?
                                    $", {instanceParameterName}" :
                                    (parameters.Count == 1) ?
                                            $", {instanceParameterName}, {parameters[0].Name}" :
                                            $", {instanceParameterName}, ({string.Join(",", parameters.Select(p => p.Name))})";
            var callParameters = $"{functionNameParameter}{callContextParameters}";


            var bodyText = $"return _client.{callMethodName}({callParameters});";
            var returnStatement = SyntaxFactory.ParseStatement(bodyText);
            var bodyBlock = SyntaxFactory.Block(returnStatement);

            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("Task<string>"), methodName)
                .WithModifiers(modifiers)
                .WithLeadingTrivia(leadingTrivia)
                .WithParameterList(parameterList)
                .WithBody(bodyBlock);
        }
    }
}
