// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;

namespace IsolatedEntities;

// three variations of the same simple entity: an entity that stores a string
// supporting get, set, and delete operations. There are slight semantic differences.

//-------------- a class-based implementation -----------------

public class StringStore
{
    [JsonInclude]
    public string Value { get; set; }

    public StringStore()
    {
        this.Value = string.Empty;
    }

    public string Get()
    {
        return this.Value;
    }

    public void Set(string value)
    {
        this.Value = value;
    }

    // Delete is implicitly defined

    [Function(nameof(StringStore))]
    public static Task Boilerplate([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<StringStore>();
    }
}

//-------------- a TaskEntity<string>-based implementation -----------------

public class StringStore2 : TaskEntity<string>
{
    public string Get()
    {
        return this.State;
    }

    public void Set(string value)
    {
        this.State = value;
    }

    protected override string InitializeState(TaskEntityOperation operation)
    {
        return string.Empty;
    }

    // Delete is implicitly defined

    [Function(nameof(StringStore2))]
    public static Task Boilerplate([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<StringStore2>();
    }
}

//-------------- a direct ITaskEntity-based implementation -----------------

class StringStore3 : ITaskEntity
{
    public ValueTask<object?> RunAsync(TaskEntityOperation operation)
    {
        switch (operation.Name)
        {
            case "set":
                operation.State.SetState((string?)operation.GetInput(typeof(string)));
                return default;

            case "get":
                // note: this does not assign a state to the entity if it does not already exist
                return new ValueTask<object?>((string?)operation.State.GetState(typeof(string)));

            case "delete":
                if (operation.State.GetState(typeof(string)) == null)
                {
                    return new ValueTask<object?>(false);
                }
                else
                {
                    operation.State.SetState(null);
                    return new ValueTask<object?>(true);
                }

            default:
                throw new NotImplementedException("no such operation");
        }
    }

    [Function(nameof(StringStore3))]
    public static Task Boilerplate([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<StringStore3>();
    }
}

 
