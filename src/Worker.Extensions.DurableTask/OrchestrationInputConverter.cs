// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
    /// Prepares orchestration input for later conversion.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <param name="input">The input.</param>
    public static void PrepareInput(FunctionContext context, object? input)
    {
        if (input is not null && !context.Items.ContainsKey(OrchestrationInputKey))
        {
            context.Items[OrchestrationInputKey] = input;
        }
    }

    /// <inheritdoc />
    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        // We only convert if:
        // 1. The "Source" is null - IE: there are no declared binding parameters.
        // 2. We have a cached input.
        // 3. The TargetType matches our cached type.
        // If these are met, then we assume this parameter is the orchestration input.
        if (context.Source is null
            && context.FunctionContext.Items.TryGetValue(OrchestrationInputKey, out object value)
            && context.TargetType == value?.GetType())
        {
            // Remove this from the items so we bind this only once.
            context.FunctionContext.Items.Remove(OrchestrationInputKey);
            return new(ConversionResult.Success(value));
        }

        return new(ConversionResult.Unhandled());
    }
}
