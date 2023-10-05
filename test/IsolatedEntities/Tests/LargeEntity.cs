// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace IsolatedEntities.Tests
{
    /// <summary>
    /// validates a simple entity scenario where an entity's state is
    /// larger than what fits into Azure table rows.
    /// </summary>
    internal class LargeEntity : Test
    {
        public override async Task RunAsync(TestContext context)
        {
            var entityId = new EntityInstanceId(nameof(StringStore2), Guid.NewGuid().ToString().Substring(0, 8));
            string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(nameof(LargeEntityOrchestration), entityId);

            // wait for completion of the orchestration
            {
                var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
                Assert.Equal("ok", metadata.ReadOutputAs<string>());
            }

            // read untyped without including state
            {
                EntityMetadata? metadata = await context.Client.Entities.GetEntityAsync(entityId, includeState: false, context.CancellationToken);
                Assert.NotNull(metadata);
                Assert.Throws<InvalidOperationException>(() => metadata!.State);
            }

            // read untyped including state
            {
                EntityMetadata? metadata = await context.Client.Entities.GetEntityAsync(entityId, includeState:true, context.CancellationToken);
                Assert.NotNull(metadata);
                Assert.NotNull(metadata!.State);
                Assert.Equal(100000, metadata!.State.ReadAs<string>().Length);
            }

            // read typed without including state
            {
                EntityMetadata<string>? metadata = await context.Client.Entities.GetEntityAsync<string>(entityId, includeState: false, context.CancellationToken);
                Assert.NotNull(metadata);
                Assert.Throws<InvalidOperationException>(() => metadata!.State);
            }

            // read typed including state
            {
                EntityMetadata<string>? metadata = await context.Client.Entities.GetEntityAsync<string>(entityId, includeState: true, context.CancellationToken);
                Assert.NotNull(metadata);
                Assert.NotNull(metadata!.State);
                Assert.Equal(100000, metadata!.State.Length);
            }   
        }

        [Function(nameof(LargeEntityOrchestration))]
        public static async Task<string> LargeEntityOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var entityId = context.GetInput<EntityInstanceId>();
            string content = new string('.', 100000);
            
            await context.Entities.CallEntityAsync(entityId, "set", content);

            var result = await context.Entities.CallEntityAsync<string>(entityId, "get");

            if (result != content)
            {
                return $"fail: wrong entity state";
            }

            return "ok";
        }
    }
}
