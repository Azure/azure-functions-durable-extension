// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

/// <summary>
/// Extension methods for <see cref="DurableTaskRegistry"/>.
/// </summary>
internal static class DurableTaskRegistryExtensions
{
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>
    /// Gets the registered orchestrators from the registry.
    /// </summary>
    /// <param name="registry">The registry to get orchestrators from.</param>
    /// <returns>The current registered orchestrators.</returns>
    public static IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskOrchestrator>>> GetOrchestrators(
            this DurableTaskRegistry registry)
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        // TODO: expose this in durabletask-dotnet
        object orchestrators = registry.GetType().GetProperty("Orchestrators", Flags)!.GetValue(registry, null)!;
        return (IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskOrchestrator>>>)orchestrators;
    }

    /// <summary>
    /// Gets the registered activities from the registry.
    /// </summary>
    /// <param name="registry">The registry to get activities from.</param>
    /// <returns>The current registered activities.</returns>
    public static IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskActivity>>> GetActivities(
            this DurableTaskRegistry registry)
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        // TODO: expose this in durabletask-dotnet
        object activities = registry.GetType().GetProperty("Activities", Flags)!.GetValue(registry, null)!;
        return (IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskActivity>>>)activities;
    }

    /// <summary>
    /// Gets the registered entities from the registry.
    /// </summary>
    /// <param name="registry">The registry to get entities from.</param>
    /// <returns>The current registered entities.</returns>
    public static IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskEntity>>> GetEntities(
            this DurableTaskRegistry registry)
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        // TODO: expose this in durabletask-dotnet
        object entities = registry.GetType().GetProperty("Entities", Flags)!.GetValue(registry, null)!;
        return (IEnumerable<KeyValuePair<TaskName, Func<IServiceProvider, ITaskEntity>>>)entities;
    }
}
