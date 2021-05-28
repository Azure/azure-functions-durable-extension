// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using DurableFunctions.TypedInterfaces.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DurableFunctions.TypedInterfaces.SourceGenerator.Generators
{
    public class TypedDurableOrchestrationContextGenerator : WrapperImplementationGenerator
    {
        private static readonly string[] requiredNamespaces = new[]
        {
            "Microsoft.Azure.WebJobs.Extensions.DurableTask",
            "System",
            "System.Collections.Generic",
            "System.Net.Http",
            "System.Threading",
            "System.Threading.Tasks"
        };

        protected override INamedTypeSymbol NamedTypeSymbol { get; }
        protected override string InterfaceName => Names.ITypedDurableOrchestrationContext;
        protected override string ClassName => Names.TypedDurableOrchestrationContext;
        protected override string ContextFieldName => "_context";

        private TypedDurableOrchestrationContextGenerator(INamedTypeSymbol namedTypeSymbol) : base()
        {
            NamedTypeSymbol = namedTypeSymbol;
        }

        public static bool TryGenerate(INamedTypeSymbol namedTypeSymbol, out CompilationUnitSyntax compilationSyntax)
        {
            var generator = new TypedDurableOrchestrationContextGenerator(namedTypeSymbol);
            compilationSyntax = generator.Generate();
            return true;
        }

        protected override ConstructorDeclarationSyntax GetConstructor()
        {
            const string contextParameterName = "context";
            const string orchestrationParameterName = "orchestration";
            const string activityParameterName = "activity";

            var parameters = AsParameterList(
                AsParameter(Names.IDurableOrchestrationContext, contextParameterName),
                AsParameter(Names.ITypedDurableOrchestrationCaller, orchestrationParameterName),
                AsParameter(Names.ITypedDurableActivityCaller, activityParameterName)
            );

            var body = AsBlock(
                AsSimpleAssignmentExpression(ContextFieldName, contextParameterName),
                AsSimpleAssignmentExpression(ITypedDurableOrchestrationContextGenerator.OrchestrationPropertyName, orchestrationParameterName),
                AsSimpleAssignmentExpression(ITypedDurableOrchestrationContextGenerator.ActivityPropertyName, activityParameterName)
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
                AsPublicPropertyWithGetter(Names.ITypedDurableOrchestrationCaller, ITypedDurableOrchestrationContextGenerator.OrchestrationPropertyName),
                AsPublicPropertyWithGetter(Names.ITypedDurableActivityCaller, ITypedDurableOrchestrationContextGenerator.ActivityPropertyName)
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
