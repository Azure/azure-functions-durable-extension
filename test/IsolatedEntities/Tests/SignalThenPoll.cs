// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace IsolatedEntities;

class SignalThenPoll : Test
{
    private readonly bool direct;
    private readonly bool delayed;

    public SignalThenPoll(bool direct, bool delayed)
    {
        this.direct = direct;
        this.delayed = delayed;
    }

    public override string Name => $"{base.Name}.{(this.direct ? "Direct" : "Indirect")}.{(this.delayed ? "Delayed" : "Immediately")}";

    public override async Task RunAsync(TestContext context)
    {
        var counterEntityId = new EntityInstanceId(nameof(Counter), Guid.NewGuid().ToString().Substring(0,8));
        var relayEntityId = new EntityInstanceId("Relay", "");
        
        string pollingInstance = await context.Client.ScheduleNewOrchestrationInstanceAsync(nameof(PollingOrchestration), counterEntityId, context.CancellationToken);
        DateTimeOffset? scheduledTime = this.delayed ? DateTime.UtcNow + TimeSpan.FromSeconds(5) : null;

        if (this.direct)
        {
            await context.Client.Entities.SignalEntityAsync(
                counterEntityId, 
                "set", 
                333,
                new SignalEntityOptions() { SignalTime = scheduledTime }, 
                context.CancellationToken);
        }
        else
        {
            await context.Client.Entities.SignalEntityAsync(
                relayEntityId, 
                operationName: "", 
                input: new Relay.Input(counterEntityId, "set", 333, scheduledTime), 
                options: null, 
                context.CancellationToken);
        }

        var metadata = await context.Client.WaitForInstanceCompletionAsync(pollingInstance, true, context.CancellationToken);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("ok", metadata.ReadOutputAs<string>());

        if (this.delayed)
        {
            Assert.True(metadata.LastUpdatedAt > scheduledTime - TimeSpan.FromMilliseconds(100));
        }

        int counterState = await context.WaitForEntityStateAsync<int>(
            counterEntityId,
            timeout: default);

        Assert.Equal(333, counterState);
    }

    [Function(nameof(PollingOrchestration))]
    public static async Task<string> PollingOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // read entity id from input
        var entityId = context.GetInput<EntityInstanceId>();
        DateTime startTime = context.CurrentUtcDateTime;

        while (context.CurrentUtcDateTime < startTime + TimeSpan.FromSeconds(30))
        {
            var result = await context.Entities.CallEntityAsync<int>(entityId, "get");

            if (result != 0)
            {
                if (result == 333)
                {
                    return "ok";
                }
                else
                {
                    return $"fail: wrong entity state: expected 333, got {result}";
                }
            }

            await context.CreateTimer(DateTime.UtcNow + TimeSpan.FromSeconds(1), CancellationToken.None);
        }

        return "timed out while waiting for entity to have state";
    }
}