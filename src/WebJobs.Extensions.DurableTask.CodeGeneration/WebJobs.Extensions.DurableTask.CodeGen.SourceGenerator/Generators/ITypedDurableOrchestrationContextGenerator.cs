﻿// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Utils;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Generators
{
    public class ITypedDurableOrchestrationContextGenerator : WrapperInterfaceGenerator
    {
        public const string OrchestrationPropertyName = "Orchestration";
        public const string ActivityPropertyName = "Activity";

        private static readonly string[] requiredNamespaces = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask"
        };

        protected override INamedTypeSymbol NamedTypeSymbol { get; }
        protected override string InterfaceName => Names.ITypedDurableOrchestrationContext;

        private ITypedDurableOrchestrationContextGenerator(INamedTypeSymbol symbol)
        {
            NamedTypeSymbol = symbol;
        }

        public static bool TryGenerate(INamedTypeSymbol namedTypeSymbol, out CompilationUnitSyntax compilationSyntax)
        {
            compilationSyntax = null;

            if (namedTypeSymbol.Name != Names.IDurableOrchestrationContext)
                return false;

            var generator = new ITypedDurableOrchestrationContextGenerator(namedTypeSymbol);
            compilationSyntax = generator.Generate();

            return true;
        }

        protected override SyntaxList<UsingDirectiveSyntax> GetAdditionalUsings()
        {
            return SyntaxFactory.List(
                requiredNamespaces.Select(AsUsing)
            );
        }

        protected override List<PropertyDeclarationSyntax> GetAdditionalProperties()
        {
            return new List<PropertyDeclarationSyntax>()
            {
                AsPropertyWithGetter(Names.ITypedDurableOrchestrationCaller, OrchestrationPropertyName),
                AsPropertyWithGetter(Names.ITypedDurableActivityCaller, ActivityPropertyName)
            };
        }
    }
}
