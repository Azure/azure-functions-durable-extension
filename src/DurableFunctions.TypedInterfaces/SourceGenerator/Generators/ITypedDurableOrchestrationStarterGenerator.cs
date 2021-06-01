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
    class ITypedDurableOrchestrationStarterGenerator : BaseGenerator
    {
        private static readonly string[] requiredUsings = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
            "System.Threading.Tasks"
        };

        private List<DurableFunction> functions;

        private ITypedDurableOrchestrationStarterGenerator(List<DurableFunction> functions) : base()
        {
            this.functions = functions;
        }

        public static bool TryGenerate(List<DurableFunction> functions, out CompilationUnitSyntax compilationSyntax)
        {
            var generator = new ITypedDurableOrchestrationStarterGenerator(functions);

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

                memberList.Add(GenerateStartMethod(function));

                requiredNamespaces.UnionWith(function.RequiredNamespaces);
            }

            var members = SyntaxFactory.List(memberList);

            var @interface = SyntaxFactory.InterfaceDeclaration(Names.ITypedDurableOrchestrationStarter)
                .WithModifiers(modifiers)
                .WithMembers(members);

            var @namespace = GenerateNamespace().AddMembers(@interface);

            var usings = AsUsings(requiredNamespaces);

            return SyntaxFactory.CompilationUnit().AddUsings(usings).AddMembers(@namespace).NormalizeWhitespace();
        }

        protected MethodDeclarationSyntax GenerateStartMethod(
            DurableFunction function
        )
        {
            const string instanceParameterName = "instance";

            var methodName = $"Start{function.Name}";

            var leadingTrivia = AsCrefSummary(function.FullTypeName);

            var parameterList = AsParameterList()
                .AddParameters(function.Parameters.Select(p => AsParameter(p.Type.ToString(), p.Name)).ToArray())
                .AddParameters(AsParameter("string", instanceParameterName).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))));

            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("Task<string>"), methodName)
                .WithLeadingTrivia(leadingTrivia)
                .WithParameterList(parameterList)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }
    }
}
