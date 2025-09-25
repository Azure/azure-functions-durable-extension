// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Execution;

internal class DurableMetadataTransformer(IOptions<DurableTaskRegistry> registry) : IFunctionMetadataTransformer
{
    private readonly DurableTaskRegistry registry = registry?.Value
        ?? throw new ArgumentNullException(nameof(registry));

    public string Name => "DurableMetadataTransformer";

    public void Transform(IList<IFunctionMetadata> original)
    {
        if (original is null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        foreach (DurableFunctionMetadata orchestrator in this.GetOrchestrators())
        {
            original.Add(orchestrator);
        }

        foreach (DurableFunctionMetadata entity in this.GetEntities())
        {
            original.Add(entity);
        }

        foreach (DurableFunctionMetadata activity in this.GetActivities())
        {
            original.Add(activity);
        }
    }

    private IEnumerable<DurableFunctionMetadata> GetOrchestrators()
    {
        foreach (KeyValuePair<TaskName, Func<IServiceProvider, ITaskOrchestrator>> kvp in this.registry.GetOrchestrators())
        {
            yield return DurableFunctionMetadata.CreateOrchestrator(kvp.Key.ToString());
        }
    }

    private IEnumerable<DurableFunctionMetadata> GetEntities()
    {
        foreach (KeyValuePair<TaskName, Func<IServiceProvider, ITaskEntity>> kvp in this.registry.GetEntities())
        {
            yield return DurableFunctionMetadata.CreateEntity(kvp.Key.ToString());
        }
    }

    private IEnumerable<DurableFunctionMetadata> GetActivities()
    {
        foreach (KeyValuePair<TaskName, Func<IServiceProvider, ITaskActivity>> kvp in this.registry.GetActivities())
        {
            yield return DurableFunctionMetadata.CreateActivity(kvp.Key.ToString());
        }
    }
}
