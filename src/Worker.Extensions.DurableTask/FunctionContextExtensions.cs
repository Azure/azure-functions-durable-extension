// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

internal static class FunctionContextExtensions
{
    /// <summary>
    /// Determines whether the function context represents a Durable Task function.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <returns>True if function is a durable task trigger, false otherwise.</returns>
    public static bool IsDurableTaskFunction(this FunctionContext context)
        => context.TryGetOrchestrationBinding(out _)
        || context.TryGetActivityBinding(out _)
        || context.TryGetEntityBinding(out _);

    /// <summary>
    /// Tries to get the orchestration trigger binding from the function context.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <param name="binding">The binding metadata if found; otherwise, null.</param>
    /// <returns>True if the orchestration trigger binding is found; otherwise, false.</returns>
    public static bool TryGetOrchestrationBinding(this FunctionContext context, [NotNullWhen(true)] out BindingMetadata? binding)
        => context.TryGetBinding(TriggerNames.Orchestration, out binding);

    /// <summary>
    /// Tries to get the orchestration trigger binding from the function context.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <param name="binding">The binding metadata if found; otherwise, null.</param>
    /// <returns>True if the orchestration trigger binding is found; otherwise, false.</returns>
    public static bool TryGetActivityBinding(this FunctionContext context, [NotNullWhen(true)] out BindingMetadata? binding)
        => context.TryGetBinding(TriggerNames.Activity, out binding);

    /// <summary>
    /// Tries to get the orchestration trigger binding from the function context.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <param name="binding">The binding metadata if found; otherwise, null.</param>
    /// <returns>True if the orchestration trigger binding is found; otherwise, false.</returns>
    public static bool TryGetEntityBinding(this FunctionContext context, [NotNullWhen(true)] out BindingMetadata? binding)
        => context.TryGetBinding(TriggerNames.Entity, out binding);

    /// <summary>
    /// Tries to get the specified trigger binding from the function context.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <param name="triggerName">The name of the trigger.</param>
    /// <param name="binding">The binding metadata if found; otherwise, null.</param>
    /// <returns>True if the specified trigger binding is found; otherwise, false.</returns>
    public static bool TryGetBinding(this FunctionContext context, string triggerName, [NotNullWhen(true)] out BindingMetadata? binding)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        foreach (BindingMetadata current in context.FunctionDefinition.InputBindings.Values)
        {
            if (string.Equals(current.Type, triggerName, StringComparison.OrdinalIgnoreCase))
            {
                binding = current;
                return true;
            }
        }

        binding = null;
        return false;
    }

    /// <summary>
    /// Gets the orchestration instance ID from the function context.
    /// </summary>
    /// <param name="context">The function context.</param>
    /// <returns>The orchestration instance ID.</returns>
    public static string GetInstanceId(this FunctionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return (string)context.BindingContext.BindingData["instanceId"]!;
    }
}
