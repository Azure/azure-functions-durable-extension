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

class MultipleLockedTransfers : Test
{
    readonly int numberEntities;

    public MultipleLockedTransfers(int numberEntities)
    {
        this.numberEntities = numberEntities;
    }

    public override string Name => $"{base.Name}.{this.numberEntities}";

    public override async Task RunAsync(TestContext context)
    {
        // create specified number of counters
        var counters = new EntityInstanceId[this.numberEntities];
        for (int i = 0; i < this.numberEntities; i++)
        {
            counters[i] = new EntityInstanceId(nameof(Counter), Guid.NewGuid().ToString().Substring(0, 8));
        }

        // in parallel, start one transfer per counter, each decrementing a counter and incrementing
        // its successor (where the last one wraps around to the first)
        // This is a pattern that would deadlock if we didn't order the lock acquisition.
        var instances = new Task<string>[this.numberEntities];
        for (int i = 0; i < this.numberEntities; i++)
        {
            instances[i] = context.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(SingleLockedTransfer.LockedTransferOrchestration),
                new[] { counters[i], counters[(i + 1) % this.numberEntities] },
                context.CancellationToken);
        }
        await Task.WhenAll(instances);


        // in parallel, wait for all transfers to complete
        var metadata = new Task<OrchestrationMetadata>[this.numberEntities];
        for (int i = 0; i < this.numberEntities; i++)
        {
            metadata[i] = context.Client.WaitForInstanceCompletionAsync(instances[i].Result, getInputsAndOutputs: true, context.CancellationToken);
        }
        await Task.WhenAll(metadata);

        // check that they all completed
        for (int i = 0; i < this.numberEntities; i++)
        {
            Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata[i].Result.RuntimeStatus);
        }

        // in parallel, read all the entity states
        var entityMetadata = new Task<EntityMetadata<int>?>[this.numberEntities];
        for (int i = 0; i < this.numberEntities; i++)
        {
            entityMetadata[i] = context.Client.Entities.GetEntityAsync<int>(counters[i], includeState: true, context.CancellationToken);
        }
        await Task.WhenAll(entityMetadata);

        // check that the counter states are all back to 0
        // (since each participated in 2 transfers, one incrementing and one decrementing)
        for (int i = 0; i < numberEntities; i++)
        {
            EntityMetadata<int>? response = entityMetadata[i].Result;
            Assert.NotNull(response); 
            Assert.Equal(0, response!.State);
        }
    }
}
