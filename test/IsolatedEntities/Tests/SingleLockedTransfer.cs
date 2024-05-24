// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
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

class SingleLockedTransfer : Test
{
    public override async Task RunAsync(TestContext context)
    {
        var counter1 = new EntityInstanceId("Counter", Guid.NewGuid().ToString().Substring(0, 8));
        var counter2 = new EntityInstanceId("Counter", Guid.NewGuid().ToString().Substring(0, 8));

        string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(nameof(LockedTransferOrchestration), new[] { counter1, counter2 }, context.CancellationToken);
        OrchestrationMetadata metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs:true, context.CancellationToken);
        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal(new[] { -1, 1 }, metadata.ReadOutputAs<int[]>());

        // validate the state of the counters
        EntityMetadata<int>? response1 = await context.Client.Entities.GetEntityAsync<int>(counter1, true, context.CancellationToken);
        EntityMetadata<int>? response2 = await context.Client.Entities.GetEntityAsync<int>(counter2, true, context.CancellationToken);
        Assert.NotNull(response1);
        Assert.NotNull(response2);
        Assert.Equal(-1, response1!.State);
        Assert.Equal(1, response2!.State);
    }

    [Function(nameof(LockedTransferOrchestration))]
    public static async Task<int[]> LockedTransferOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // read entity id from input
        var entities = context.GetInput<EntityInstanceId[]>();
        var from = entities![0];
        var to = entities![1];

        if (from.Equals(to))
        {
            throw new ArgumentException("from and to must be distinct");
        }

        ExpectSynchState(false);

        int fromBalance;
        int toBalance;

        await using (await context.Entities.LockEntitiesAsync(from, to))
        {
            ExpectSynchState(true, from, to);

            // read balances in parallel
            var t1 = context.Entities.CallEntityAsync<int>(from, "get");
            ExpectSynchState(true, to);
            var t2 = context.Entities.CallEntityAsync<int>(to, "get");
            ExpectSynchState(true);
           

            fromBalance = await t1;
            toBalance = await t2;
            ExpectSynchState(true, from, to);
 
            // modify
            fromBalance--;
            toBalance++;

            // write balances in parallel
            var t3 = context.Entities.CallEntityAsync(from, "set", fromBalance);
            ExpectSynchState(true, to);
            var t4 = context.Entities.CallEntityAsync(to, "set", toBalance);
            ExpectSynchState(true);
            await t4;
            await t3;
            ExpectSynchState(true, to, from);

        } // lock is released here

        ExpectSynchState(false);

        return new int[] { fromBalance, toBalance };

        void ExpectSynchState(bool inCriticalSection, params EntityInstanceId[] ids)
        {
            Assert.Equal(inCriticalSection, context.Entities.InCriticalSection(out var currentLocks));
            if (inCriticalSection)
            {
                Assert.Equal<string>(
                    ids.Select(i => i.ToString()).OrderBy(s => s), 
                    currentLocks!.Select(i => i.ToString()).OrderBy(s => s));
            }
        }
    }
}
