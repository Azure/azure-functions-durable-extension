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

/// <summary>
/// A stateless entity that forwards signals
/// </summary>
class Relay : ITaskEntity
{
    [Function(nameof(Relay))]
    public static Task Boilerplate([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<Relay>();
    }

    public record Input(EntityInstanceId entityInstanceId, string operationName, object? input, DateTimeOffset? scheduledTime);

    public ValueTask<object?> RunAsync(TaskEntityOperation operation)
    {
        T GetInput<T>() => (T)operation.GetInput(typeof(T))!;

        Input input = GetInput<Input>();

        operation.Context.SignalEntity(
            input.entityInstanceId, 
            input.operationName, 
            input.input,
            new SignalEntityOptions() { SignalTime = input.scheduledTime });

        return default;
    }
}
