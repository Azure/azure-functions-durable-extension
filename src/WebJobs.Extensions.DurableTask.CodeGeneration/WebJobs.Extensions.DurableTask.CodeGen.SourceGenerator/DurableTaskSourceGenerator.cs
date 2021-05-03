// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator.Generators;

namespace WebJobs.Extensions.DurableTask.CodeGeneration.SourceGenerator
{
    /// <summary>
    /// Source generator for creating helper classes based on DurableFunction usage within the project.
    /// </summary>
    [Generator]
    public class DurableTaskSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            Debug.WriteLine("Initialize source generator");
            context.RegisterForSyntaxNotifications(() => new DurableTaskSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                if (!(context.SyntaxContextReceiver is DurableTaskSyntaxReceiver receiver))
                    return;

                // Exit early if the receiver did not manage to be initialized
                if (!receiver.Initialized)
                    return;

                var parseOptions = context.ParseOptions;

                // Our overall goal is to make sure that if we can't successfully generate everything,
                // we don't generate anything at all.
                // First we will validate we can generate a CompilationUnit for all the necessary
                // generated code. If any fails, we will exit immediately and not add the units as source
                // text - so nothing will be generated. Otherwise we will add them all as source text.

                if (!IGeneratedDurableOrchestrationContextGenerator.TryGenerate(receiver.DurableOrchestrationContextTypeSymbol, out var iOrchestrationContext))
                    return;

                if (!GeneratedDurableOrchestrationContextGenerator.TryGenerate(receiver.DurableOrchestrationContextTypeSymbol, out var orchestrationContext))
                    return;

                if (!IGeneratedDurableClientGenerator.TryGenerate(receiver.DurableClientTypeSymbol, out var client))
                    return;

                if (!GeneratedDurableClientGenerator.TryGenerate(receiver.DurableClientTypeSymbol, out var iClient))
                    return;

                if (!IGeneratedDurableOrchestrationCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax iOrchestrationCaller))
                    return;

                if (!GeneratedDurableOrchestrationCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax orchestrationCaller))
                    return;

                if (!IGeneratedDurableActivityCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax iActivityCaller))
                    return;

                if (!GeneratedDurableActivityCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax activityCaller))
                    return;

                if (!IGeneratedDurableOrchestrationStarterGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax iOrchestrationStarter))
                    return;

                if (!GeneratedDurableOrchestrationStarterGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax orchestrationStarter))
                    return;

                context.AddSource("IGeneratedDurableOrchestrationContext.cs", AsSourceText(parseOptions, iOrchestrationContext));
                context.AddSource("GeneratedDurableOrchestrationContext.cs", AsSourceText(parseOptions, orchestrationContext));
                context.AddSource("IGeneratedDurableClient.cs", AsSourceText(parseOptions, client));
                context.AddSource("GeneratedDurableClient.cs", AsSourceText(parseOptions, iClient));
                context.AddSource("IGeneratedDurableOrchestrationCaller.cs", AsSourceText(parseOptions, iOrchestrationCaller));
                context.AddSource("GeneratedDurableOrchestrationCaller.cs", AsSourceText(parseOptions, orchestrationCaller));
                context.AddSource("IGeneratedDurableActivityCaller.cs", AsSourceText(parseOptions, iActivityCaller));
                context.AddSource("GeneratedDurableActivityCaller.cs", AsSourceText(parseOptions, activityCaller));
                context.AddSource($"IGeneratedDurableOrchestrationStarter.cs", AsSourceText(parseOptions, iOrchestrationStarter));
                context.AddSource($"GeneratedDurableOrchestrationStarter.cs", AsSourceText(parseOptions, orchestrationStarter));
            }
            catch (Exception ex)
            {
                // Don't let exceptions bubble back to users
                Debug.WriteLine(ex);
            }
        }

        private SourceText AsSourceText(ParseOptions parseOptions, CompilationUnitSyntax comipilationSyntax)
        {
            return SyntaxFactory.SyntaxTree(comipilationSyntax, parseOptions, encoding: Encoding.UTF8).GetText();
        }
    }
}
