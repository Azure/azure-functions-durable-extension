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

namespace IsolatedEntities;

class Counter : TaskEntity<int>
{
    public void Increment()
    {
        this.State++;
    }

    public void Add(int amount)
    {
        this.State += amount;
    }

    public int Get()
    {
        return this.State;
    }

    public void Set(int value)
    {
        this.State = value;
    }

    [Function(nameof(Counter))]
    public static Task Boilerplate([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<Counter>();
    }
}
