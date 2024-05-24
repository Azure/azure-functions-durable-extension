// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DurableTask.Core.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace IsolatedEntities;

class CallAndDelete : Test
{
    private readonly Type stringStoreType;

    public CallAndDelete(Type stringStoreType)
    {
        this.stringStoreType = stringStoreType;
    }

    public override string Name => $"{base.Name}.{this.stringStoreType.Name}";

    public override async Task RunAsync(TestContext context)
    {
        var entityId = new EntityInstanceId(this.stringStoreType.Name, Guid.NewGuid().ToString());
        string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(nameof(CallAndDeleteOrchestration), entityId);
        var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("ok", metadata.ReadOutputAs<string>());

        // check that entity was deleted
        var entityMetadata = await context.Client.Entities.GetEntityAsync(entityId);
        Assert.Null(entityMetadata);
    }

    static bool GetOperationInitializesEntity(EntityInstanceId entityInstanceId) 
        => !string.Equals(entityInstanceId.Name, nameof(StringStore3).ToLowerInvariant(), StringComparison.InvariantCulture);

    static bool DeleteReturnsBoolean(EntityInstanceId entityInstanceId) 
        => string.Equals(entityInstanceId.Name, nameof(StringStore3).ToLowerInvariant(), StringComparison.InvariantCulture);

    [Function(nameof(CallAndDeleteOrchestration))]
    public static async Task<string> CallAndDeleteOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        EntityInstanceId entityId = context.GetInput<EntityInstanceId>();
        await context.Entities.CallEntityAsync(entityId, "set", "333");

        string value = await context.Entities.CallEntityAsync<string>(entityId, "get");
        Assert.Equal("333", value);

        if (DeleteReturnsBoolean(entityId))
        {
            bool deleted = await context.Entities.CallEntityAsync<bool>(entityId, "delete");
            Assert.True(deleted);

            bool deletedAgain = await context.Entities.CallEntityAsync<bool>(entityId, "delete");
            Assert.False(deletedAgain);
        }
        else
        {
            await context.Entities.CallEntityAsync(entityId, "delete");
        }

        string getValue = await context.Entities.CallEntityAsync<string>(entityId, "get");
        if (GetOperationInitializesEntity(entityId))
        {
            Assert.Equal("", getValue);
        }
        else
        {
            Assert.Equal(null, getValue);
        }

        if (DeleteReturnsBoolean(entityId))
        {
            bool deletedAgain = await context.Entities.CallEntityAsync<bool>(entityId, "delete");
            if (GetOperationInitializesEntity(entityId))
            {
                Assert.True(deletedAgain);
            }
            else
            {
                Assert.False(deletedAgain);
            }
        }
        else
        {
            await context.Entities.CallEntityAsync(entityId, "delete");
        }

        return "ok";
    }
}