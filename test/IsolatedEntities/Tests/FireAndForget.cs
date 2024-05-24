// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace IsolatedEntities;

/// <summary>
/// Scenario that starts a new orchestration from an entity.
/// </summary>
class FireAndForget : Test
{
    private readonly int? delay;

    public FireAndForget(int? delay)
    {
        this.delay = delay;
    }

    public override string Name => $"{base.Name}.{(this.delay.HasValue ? "Delay" + this.delay.Value.ToString() : "NoDelay")}";

    public override async Task RunAsync(TestContext context)
    {
        string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(nameof(LaunchOrchestrationFromEntity), this.delay, context.CancellationToken);
        OrchestrationMetadata metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true, context.CancellationToken);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);

        string? launchedId = metadata.ReadOutputAs<string>();
        Assert.NotNull(launchedId);
        var launchedMetadata = await context.Client.GetInstanceAsync(launchedId!, getInputsAndOutputs: true, context.CancellationToken);
        Assert.NotNull(launchedMetadata);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, launchedMetadata!.RuntimeStatus);
        Assert.Equal("ok", launchedMetadata!.ReadOutputAs<string>());
    }

    [Function(nameof(LaunchOrchestrationFromEntity))]
    public static async Task<string> LaunchOrchestrationFromEntity([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        int? delay = context.GetInput<int?>();

        var entityId = new EntityInstanceId("Launcher", context.NewGuid().ToString().Substring(0, 8));

        if (delay.HasValue)
        {
            await context.Entities.CallEntityAsync(entityId, "launch", context.CurrentUtcDateTime + TimeSpan.FromSeconds(delay.Value));
        }
        else
        {
            await context.Entities.CallEntityAsync(entityId, "launch");
        }
        
        while (true)
        {
            string? launchedOrchestrationId = await context.Entities.CallEntityAsync<string>(entityId, "get");

            if (launchedOrchestrationId != null)
            {
                return launchedOrchestrationId;
            }

            await context.CreateTimer(DateTime.UtcNow + TimeSpan.FromSeconds(1), CancellationToken.None);
        }
    }

    [Function(nameof(SignallingOrchestration))]
    public static async Task<string> SignallingOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = context.GetInput<EntityInstanceId>();

        await context.CreateTimer(DateTime.UtcNow + TimeSpan.FromSeconds(.2), CancellationToken.None);

        await context.Entities.SignalEntityAsync(entityId, "done");

        return "ok";
    }
}
