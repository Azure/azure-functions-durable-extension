// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DurableTask.Core.Entities.OperationFormat;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace IsolatedEntities;

class CleanOrphanedLock : Test
{

    public override async Task RunAsync(TestContext context)
    {
        // clean the storage before starting the test so we start from a clean slate
        await context.Client.Entities.CleanEntityStorageAsync(new());

        DateTime startTime = DateTime.UtcNow;

        // construct unique names for this test
        string prefix = Guid.NewGuid().ToString("N").Substring(0, 6);
        var orphanedEntityId = new EntityInstanceId(nameof(Counter), $"{prefix}-orphaned");
        var orchestrationA = $"{prefix}-A";
        var orchestrationB = $"{prefix}-B";

        // start an orchestration A that acquires the lock and then waits forever
        await context.Client.ScheduleNewOrchestrationInstanceAsync(
            nameof(BadLockerOrchestration),
            orphanedEntityId,
            new StartOrchestrationOptions() {  InstanceId = orchestrationA },
            context.CancellationToken);
        await context.Client.WaitForInstanceStartAsync(orchestrationA, context.CancellationToken);

        // start an orchestration B that queues behind A for the lock (and thus gets stuck)
       await context.Client.ScheduleNewOrchestrationInstanceAsync(
           nameof(UnluckyWaiterOrchestration),
           orphanedEntityId,
           new StartOrchestrationOptions() { InstanceId = orchestrationB },
           context.CancellationToken);
        await context.Client.WaitForInstanceStartAsync(orchestrationB, context.CancellationToken);

        // brutally and unsafely purge the running orchestrationA from storage, leaving the lock orphaned
        await context.Client.PurgeInstanceAsync(orchestrationA);

        // check the status of the entity to confirm that the lock is held
        List<EntityMetadata> results = await context.Client.Entities.GetAllEntitiesAsync(
            new Microsoft.DurableTask.Client.Entities.EntityQuery()
            {
                InstanceIdStartsWith = orphanedEntityId.ToString(),
                IncludeStateless = true,
                IncludeState = true,
            }).ToListAsync();
        Assert.Equal(1, results.Count);
        Assert.Equal(orphanedEntityId, results[0].Id);
        Assert.False(results[0].IncludesState);
        Assert.True(results[0].LastModifiedTime > startTime);
        Assert.Equal(orchestrationA, results[0].LockedBy);
        Assert.Equal(1, results[0].BacklogQueueSize); // that's the request that is waiting for the lock
        DateTimeOffset lastModified = results[0].LastModifiedTime;

        // clean the entity storage to remove the orphaned lock
        var cleaningResponse = await context.Client.Entities.CleanEntityStorageAsync(new());
        Assert.Equal(0, cleaningResponse.EmptyEntitiesRemoved);
        Assert.Equal(1, cleaningResponse.OrphanedLocksReleased);

        // now wait for orchestration B to finish
        OrchestrationMetadata metadata = await context.Client.WaitForInstanceCompletionAsync(orchestrationB, getInputsAndOutputs: true, context.CancellationToken);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("ok", metadata.ReadOutputAs<string>());

        // clean the entity storage again, this time there should be nothing left to clean
        cleaningResponse = await context.Client.Entities.CleanEntityStorageAsync(new());
        Assert.Equal(0, cleaningResponse.EmptyEntitiesRemoved);
        Assert.Equal(0, cleaningResponse.OrphanedLocksReleased);

        // check the status of the entity to confirm that the lock is no longer held
        results = await context.Client.Entities.GetAllEntitiesAsync(
            new Microsoft.DurableTask.Client.Entities.EntityQuery()
            {
                InstanceIdStartsWith = orphanedEntityId.ToString(),
                IncludeStateless = true,
                IncludeState = true,
            }).ToListAsync();
        Assert.Equal(1, results.Count);
        Assert.Equal(orphanedEntityId, results[0].Id);
        Assert.True(results[0].IncludesState);
        Assert.Equal(1, results[0].State.ReadAs<int>());
        Assert.True(results[0].LastModifiedTime > lastModified);
        Assert.Null(results[0].LockedBy);
        Assert.Equal(0, results[0].BacklogQueueSize);
    }

    [Function(nameof(BadLockerOrchestration))]
    public static async Task<string> BadLockerOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // read entity id from input
        var entityId = context.GetInput<EntityInstanceId>();

        await using (await context.Entities.LockEntitiesAsync(entityId))
        {
            await context.CreateTimer(DateTime.UtcNow + TimeSpan.FromDays(365), CancellationToken.None);
        }

        // will never reach the end here because we get purged in the middle
        return "ok";
    }

    [Function(nameof(UnluckyWaiterOrchestration))]
    public static async Task<string> UnluckyWaiterOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // read entity id from input
        var entityId = context.GetInput<EntityInstanceId>();

        await using (await context.Entities.LockEntitiesAsync(entityId))
        {
            await context.Entities.CallEntityAsync(entityId, "increment");

            // we got the entity
            return "ok";
        }
    }
}