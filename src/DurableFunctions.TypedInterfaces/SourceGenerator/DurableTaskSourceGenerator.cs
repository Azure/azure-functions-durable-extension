// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using DurableFunctions.TypedInterfaces.SourceGenerator.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DurableFunctions.TypedInterfaces
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

                if (!ITypedDurableOrchestrationContextGenerator.TryGenerate(receiver.DurableOrchestrationContextTypeSymbol, out var iOrchestrationContext))
                    return;

                if (!TypedDurableOrchestrationContextGenerator.TryGenerate(receiver.DurableOrchestrationContextTypeSymbol, out var orchestrationContext))
                    return;

                if (!ITypedDurableClientGenerator.TryGenerate(receiver.DurableClientTypeSymbol, out var client))
                    return;

                if (!TypedDurableClientGenerator.TryGenerate(receiver.DurableClientTypeSymbol, out var iClient))
                    return;

                if (!ITypedDurableOrchestrationCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax iOrchestrationCaller))
                    return;

                if (!TypedDurableOrchestrationCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax orchestrationCaller))
                    return;

                if (!ITypedDurableActivityCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax iActivityCaller))
                    return;

                if (!TypedDurableActivityCallerGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax activityCaller))
                    return;

                if (!ITypedDurableOrchestrationStarterGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax iOrchestrationStarter))
                    return;

                if (!TypedDurableOrchestrationStarterGenerator.TryGenerate(receiver.DurableFunctions, out CompilationUnitSyntax orchestrationStarter))
                    return;

                context.AddSource("ITypedDurableOrchestrationContext.cs", AsSourceText(parseOptions, iOrchestrationContext));
                context.AddSource("TypedDurableOrchestrationContext.cs", AsSourceText(parseOptions, orchestrationContext));
                context.AddSource("ITypedDurableClient.cs", AsSourceText(parseOptions, client));
                context.AddSource("TypedDurableClient.cs", AsSourceText(parseOptions, iClient));
                context.AddSource("ITypedDurableOrchestrationCaller.cs", AsSourceText(parseOptions, iOrchestrationCaller));
                context.AddSource("TypedDurableOrchestrationCaller.cs", AsSourceText(parseOptions, orchestrationCaller));
                context.AddSource("ITypedDurableActivityCaller.cs", AsSourceText(parseOptions, iActivityCaller));
                context.AddSource("TypedDurableActivityCaller.cs", AsSourceText(parseOptions, activityCaller));
                context.AddSource("ITypedDurableOrchestrationStarter.cs", AsSourceText(parseOptions, iOrchestrationStarter));
                context.AddSource("TypedDurableOrchestrationStarter.cs", AsSourceText(parseOptions, orchestrationStarter));
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
