// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Converters;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// Binds the input for an orchestration, if an input parameter is present.
/// </summary>
internal class OrchestrationInputConverter : IInputConverter
{
    /// <summary>
    /// The key we cache the prepared orchestration input in <see cref="FunctionContext.Items" />.
    /// </summary>
    private const string OrchestrationInputKey = "__orchestrationInput__";

    /// <summary>
    /// Gets the input context for the current orchestration invocation. This context can be later
    /// used to "seed" the input for later conversion.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <returns>The input context.</returns>
    public static InputContext GetInputContext(FunctionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        FunctionDefinition definition = context.FunctionDefinition;
        foreach (FunctionParameter parameter in definition.Parameters)
        {
            if (!IgnoredInputType(parameter.Type)
                && !definition.InputBindings.ContainsKey(parameter.Name)
                && !definition.OutputBindings.ContainsKey(parameter.Name))
            {
                // We take the first parameter we encounter which is not an explicitly ignored type
                // and has exactly 0 bindings (in or out) as our input candidate.
                return new(parameter.Type, context);
            }
        }

        return new(null, context);
    }

    /// <inheritdoc />
    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // We only convert if:
        // 1. The "Source" is null - IE: there are no declared binding parameters.
        // 2. We have a cached input.
        // 3. The TargetType matches our cached type.
        // If these are met, then we assume this parameter is the orchestration input.
        if (context.Source is null
            && context.FunctionContext.Items.TryGetValue(OrchestrationInputKey, out object? value)
            && context.TargetType == value?.GetType())
        {
            // Remove this from the items so we bind this only once.
            context.FunctionContext.Items.Remove(OrchestrationInputKey);
            return new(ConversionResult.Success(value));
        }

        return new(ConversionResult.Unhandled());
    }

    private static bool IgnoredInputType(Type type)
    {
        // These are input types we know other converters handle.
        // TODO: is there a more concrete way we can determine if a type is already handled?
        return type == typeof(FunctionContext) || type == typeof(CancellationToken);
    }

    /// <summary>
    /// The context for an orchestration input.
    /// </summary>
    public class InputContext
    {
        private readonly FunctionContext context;
        private readonly Type? type;

        public InputContext(Type? type, FunctionContext context)
        {
            this.type = type;
            this.context = context;
        }

        /// <summary>
        /// Gets the input type. Will be <see cref="object" /> if none found.
        /// </summary>
        public Type Type => this.type ?? typeof(object);

        /// <summary>
        /// Prepares orchestration input for later conversion.
        /// </summary>
        /// <param name="input">The input.</param>
        public void PrepareInput(object? input)
        {
            // Short circuit if there is no input for our converter to handle, or if we have already prepared it.
            if (this.type is null || input is null || this.context.Items.ContainsKey(OrchestrationInputKey))
            {
                return;
            }

            this.context.Items[OrchestrationInputKey] = input;
        }
    }
}
