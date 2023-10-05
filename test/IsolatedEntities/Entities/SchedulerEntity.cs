// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace IsolatedEntities;

class SchedulerEntity : ITaskEntity
{
    private readonly ILogger logger;

    public SchedulerEntity(ILogger<SchedulerEntity> logger)
    {
        this.logger = logger;
    }

    [Function(nameof(SchedulerEntity))]
    public static Task Boilerplate([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<SchedulerEntity>();
    }

    public ValueTask<object?> RunAsync(TaskEntityOperation operation)
    {
        this.logger.LogInformation("{entityId} received {operationName} signal", operation.Context.Id, operation.Name);

        List<string> state = (List<string>?)operation.State.GetState(typeof(List<string>)) ?? new List<string>();

        if (state.Contains(operation.Name))
        {
            this.logger.LogError($"duplicate: {operation.Name}");
        }
        else
        {
            state.Add(operation.Name);
        }

        return default;
    }
}
