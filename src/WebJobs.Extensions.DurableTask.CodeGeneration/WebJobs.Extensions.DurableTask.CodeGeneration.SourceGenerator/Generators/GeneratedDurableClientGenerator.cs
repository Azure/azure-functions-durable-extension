// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Utils;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Generators
{
    public class GeneratedDurableClientGenerator : WrapperImplementationGenerator
    {
        private const string OrchestrationPropertyName = "Orchestration";

        private static readonly string[] requiredNamespaces = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
            "System",
            "System.Collections.Generic",
            "System.Net.Http",
            "System.Threading",
            "System.Threading.Tasks",
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc",
            "DurableTask.Core"
        };

        protected override INamedTypeSymbol NamedTypeSymbol { get; }
        protected override string InterfaceName => Names.IGeneratedDurableClient;
        protected override string ClassName => Names.GeneratedDurableClient;
        protected override string ContextFieldName => "_client";

        private GeneratedDurableClientGenerator(INamedTypeSymbol namedTypeSymbol)
        {
            NamedTypeSymbol = namedTypeSymbol;
        }

        public static bool TryGenerate(INamedTypeSymbol namedTypeSymbol, out CompilationUnitSyntax compilationSyntax)
        {
            compilationSyntax = null;

            if (namedTypeSymbol.Name != Names.IDurableClient)
                return false;

            var generator = new GeneratedDurableClientGenerator(namedTypeSymbol);
            compilationSyntax = generator.Generate();
            return true;
        }

        protected override ConstructorDeclarationSyntax GetConstructor()
        {
            const string contextParameterName = "client";
            const string orchestrationParameterName = "orchestration";

            var parameters = AsParameterList(
                AsParameter(Names.IDurableClient, contextParameterName),
                AsParameter(Names.IGeneratedDurableOrchestrationStarter, orchestrationParameterName)
            );

            var body = AsBlock(
                AsSimpleAssignmentExpression(ContextFieldName, contextParameterName),
                AsSimpleAssignmentExpression(OrchestrationPropertyName, orchestrationParameterName)
            );

            return SyntaxFactory.ConstructorDeclaration(ClassName)
                .WithModifiers(AsModifierList(SyntaxKind.PublicKeyword))
                .WithParameterList(parameters)
                .WithBody(body);
        }

        protected override PropertyDeclarationSyntax[] GetAdditionalProperties()
        {
            return new[]
            {
                AsPublicPropertyWithGetter(Names.IGeneratedDurableOrchestrationStarter, OrchestrationPropertyName)
            };
        }

        protected override SyntaxList<UsingDirectiveSyntax> GetAdditionalUsings()
        {
            return SyntaxFactory.List(
                requiredNamespaces.Select(AsUsing)
            );
        }
    }
}
