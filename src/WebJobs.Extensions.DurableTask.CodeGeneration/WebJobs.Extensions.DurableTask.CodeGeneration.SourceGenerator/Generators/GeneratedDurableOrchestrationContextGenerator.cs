// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Utils;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Generators
{
    public class GeneratedDurableOrchestrationContextGenerator : WrapperImplementationGenerator
    {
        private const string OrchestrationCallerPropertyName = "Orchestration";
        private const string ActivityCallerPropertyName = "Activity";

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
        protected override string InterfaceName => Names.IGeneratedDurableOrchestrationContext;
        protected override string ClassName => Names.GeneratedDurableOrchestrationContext;
        protected override string ContextFieldName => "_context";

        private GeneratedDurableOrchestrationContextGenerator(INamedTypeSymbol namedTypeSymbol) : base()
        {
            NamedTypeSymbol = namedTypeSymbol;
        }

        public static bool TryGenerate(INamedTypeSymbol namedTypeSymbol, out CompilationUnitSyntax compilationSyntax)
        {
            var generator = new GeneratedDurableOrchestrationContextGenerator(namedTypeSymbol);
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
                AsParameter(Names.IGeneratedDurableOrchestrationCaller, orchestrationParameterName),
                AsParameter(Names.IGeneratedDurableActivityCaller, activityParameterName)
            );

            var body = AsBlock(
                AsSimpleAssignmentExpression(ContextFieldName, contextParameterName),
                AsSimpleAssignmentExpression(OrchestrationCallerPropertyName, orchestrationParameterName),
                AsSimpleAssignmentExpression(ActivityCallerPropertyName, activityParameterName)
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
                AsPublicPropertyWithGetter(Names.IGeneratedDurableOrchestrationCaller, OrchestrationCallerPropertyName),
                AsPublicPropertyWithGetter(Names.IGeneratedDurableActivityCaller, ActivityCallerPropertyName)
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
