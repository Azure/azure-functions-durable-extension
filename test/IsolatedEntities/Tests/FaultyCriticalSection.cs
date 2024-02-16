// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DurableTask.Core.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsolatedEntities;

class FaultyCriticalSection : Test
{
    public override async Task RunAsync(TestContext context)
    {
        var entityId = new EntityInstanceId(nameof(Counter), Guid.NewGuid().ToString());
        string orchestrationName = nameof(FaultyCriticalSectionOrchestration);
       
        // run the critical section but fail in the middle
        {
            string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(orchestrationName, new FaultyCriticalSectionOrchestration.Input(entityId, true));
            var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);
            Assert.Equal(OrchestrationRuntimeStatus.Failed, metadata.RuntimeStatus);
            Assert.True(metadata.SerializedOutput!.Contains("KABOOM"));
        }

        // run the critical section again without failing this time - this will time out if lock was not released properly.
        {
            string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(orchestrationName, new FaultyCriticalSectionOrchestration.Input(entityId, false));
            var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);
            Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
            Assert.Equal("ok", metadata.ReadOutputAs<string>());
        }
    }
}

class FaultyCriticalSectionOrchestration
{ 
    readonly ILogger logger;

    public record Input(EntityInstanceId EntityInstanceId, bool Fail);

    public FaultyCriticalSectionOrchestration(ILogger<CallFaultyEntityOrchestration> logger)
    {
        this.logger = logger;
    }

    [Function(nameof(FaultyCriticalSectionOrchestration))]
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // read input
        var input = context.GetInput<Input>()!;

        await using (await context.Entities.LockEntitiesAsync(input.EntityInstanceId))
        {
            if (input.Fail)
            {
                throw new Exception("KABOOM");
            }
        }

        return "ok";
    }
}