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
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsolatedEntities;

class EntityQueries2 : Test
{
    public override async Task RunAsync(TestContext context)
    {
        // ----- first, delete all already-existing entities in storage to ensure queries have predictable results
        context.Logger.LogInformation("deleting existing entities");

        // we simply delete all the running instances which does also purge all entities
        await context.Client.PurgeAllInstancesAsync(
            new PurgeInstancesFilter()
            {
                CreatedFrom = DateTime.MinValue,
                Statuses = new OrchestrationRuntimeStatus[] { OrchestrationRuntimeStatus.Running }
            },
            context.CancellationToken);

        var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
        var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

        // check that everything is completely blank, there are no entities, not even stateless ones

        var e = context.Client.Entities.GetAllEntitiesAsync(new EntityQuery() { IncludeStateless = true }).GetAsyncEnumerator();
        Assert.False(await e.MoveNextAsync());

        // ----- next, run a number of orchestrations in order to create and/or delete specific instances
        context.Logger.LogInformation("creating and deleting entities");

        List<string> orchestrations = new List<string>()
        {
            nameof(SignalAndCall.SignalAndCallOrchestration),
            nameof(CallAndDelete.CallAndDeleteOrchestration),
            nameof(SignalAndCall.SignalAndCallOrchestration),
            nameof(CallAndDelete.CallAndDeleteOrchestration),
            nameof(SignalAndCall.SignalAndCallOrchestration),
            nameof(CallAndDelete.CallAndDeleteOrchestration),
            nameof(SignalAndCall.SignalAndCallOrchestration),
            nameof(CallAndDelete.CallAndDeleteOrchestration),
        };

        List<EntityInstanceId> entityIds = new List<EntityInstanceId>()
        {
            new EntityInstanceId("StringStore", "foo"),
            new EntityInstanceId("StringStore2", "bar"),
            new EntityInstanceId("StringStore2", "baz"),
            new EntityInstanceId("StringStore2", "foo"),
            new EntityInstanceId("StringStore2", "ffo"),
            new EntityInstanceId("StringStore2", "zzz"),
            new EntityInstanceId("StringStore2", "aaa"),
            new EntityInstanceId("StringStore2", "bbb"),
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, entityIds.Count),
            context.CancellationToken,
            async (int i, CancellationToken cancellation) =>
            {
                string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(orchestrations[i], entityIds[i]);
                await context.Client.WaitForInstanceCompletionAsync(instanceId, cancellation);
            });

        await Task.Delay(TimeSpan.FromSeconds(3)); // accounts for delay in updating instance tables

        // ----- use a collection of (query, validation function) pairs
        context.Logger.LogInformation("starting query tests");

        var tests = new (EntityQuery query, Action<IList<EntityMetadata>> test)[]
        {
            (new EntityQuery
            {
            },
            result =>
            {
                Assert.Equal(4, result.Count());
            }),

            (new EntityQuery
            {
                IncludeStateless = true,
            },
            result =>
            {
                Assert.Equal(8, result.Count()); // TODO this is provider-specific
            }),


            (new EntityQuery
            {
                PageSize = 3,
            },
            result =>
            {
                Assert.Equal(4, result.Count());
            }),

            (new EntityQuery
            {
                IncludeStateless = true,
                PageSize = 3,
            },
            result =>
            {
                Assert.Equal(8, result.Count()); // TODO this is provider-specific
            }),
        };

        foreach (var item in tests)
        {
            List<EntityMetadata> results = new List<EntityMetadata>();
            await foreach (var element in context.Client.Entities.GetAllEntitiesAsync(item.query))
            {
                results.Add(element);
            }

            item.test(results);
        }

        // ----- remove the 4 deleted entities whose metadata still lingers in Azure Storage provider
        // TODO this is provider-specific

        context.Logger.LogInformation("starting storage cleaning");

        var cleaningResponse = await context.Client.Entities.CleanEntityStorageAsync(new CleanEntityStorageRequest());

        Assert.Equal(4, cleaningResponse.EmptyEntitiesRemoved);
        Assert.Equal(0, cleaningResponse.OrphanedLocksReleased);
    }
}