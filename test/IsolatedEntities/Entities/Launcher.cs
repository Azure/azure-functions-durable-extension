// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace IsolatedEntities;

class Launcher
{
    public string? OrchestrationInstanceId { get; set; }

    public DateTime? ScheduledTime { get; set; }

    public bool IsDone { get; set; }

    public string? ErrorMessage { get; set; }

    public void Launch(TaskEntityContext context, DateTime? scheduledTime = null)
    {
        this.OrchestrationInstanceId = context.ScheduleNewOrchestration(
            nameof(FireAndForget.SignallingOrchestration),
            context.Id,
            new StartOrchestrationOptions(StartAt: scheduledTime));
    }

    public string? Get()
    {
        if (this.ErrorMessage != null)
        {
            throw new Exception(this.ErrorMessage);
        }
        return this.IsDone ? this.OrchestrationInstanceId : null;
    }

    public void Done()
    {
        this.IsDone = true;

        if (this.ScheduledTime != null)
        {
            DateTime now = DateTime.UtcNow;
            if (now < this.ScheduledTime)
            {
                this.ErrorMessage = $"delay was too short, expected >= {this.ScheduledTime},  actual = {now}";
            }
        }
    }

    [Function(nameof(Launcher))]
    public static Task Boilerplate([EntityTrigger] TaskEntityDispatcher dispatcher)
    {
        return dispatcher.DispatchAsync<Launcher>();
    }
}
