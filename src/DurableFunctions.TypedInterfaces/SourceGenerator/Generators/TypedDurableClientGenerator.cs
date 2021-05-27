// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public class TypedDurableClientGenerator : WrapperImplementationGenerator
    {
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
        protected override string InterfaceName => Names.ITypedDurableClient;
        protected override string ClassName => Names.TypedDurableClient;
        protected override string ContextFieldName => "_client";

        private TypedDurableClientGenerator(INamedTypeSymbol namedTypeSymbol)
        {
            NamedTypeSymbol = namedTypeSymbol;
        }

        public static bool TryGenerate(INamedTypeSymbol namedTypeSymbol, out CompilationUnitSyntax compilationSyntax)
        {
            compilationSyntax = null;

            if (namedTypeSymbol.Name != Names.IDurableClient)
                return false;

            var generator = new TypedDurableClientGenerator(namedTypeSymbol);
            compilationSyntax = generator.Generate();
            return true;
        }

        protected override ConstructorDeclarationSyntax GetConstructor()
        {
            const string contextParameterName = "client";
            const string orchestrationParameterName = "orchestration";

            var parameters = AsParameterList(
                AsParameter(Names.IDurableClient, contextParameterName),
                AsParameter(Names.ITypedDurableOrchestrationStarter, orchestrationParameterName)
            );

            var body = AsBlock(
                AsSimpleAssignmentExpression(ContextFieldName, contextParameterName),
                AsSimpleAssignmentExpression(ITypedDurableClientGenerator.OrchestrationPropertyName, orchestrationParameterName)
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
                AsPublicPropertyWithGetter(Names.ITypedDurableOrchestrationStarter, ITypedDurableClientGenerator.OrchestrationPropertyName)
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
