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

class SetAndGet : Test
{
    public override async Task RunAsync(TestContext context)
    {
        var entityId = new EntityInstanceId(nameof(Counter), Guid.NewGuid().ToString());

        // entity should not yet exist
        EntityMetadata<int>? result = await context.Client.Entities.GetEntityAsync<int>(entityId);
        Assert.Null(result);

        // entity should still not exist
        result = await context.Client.Entities.GetEntityAsync<int>(entityId, includeState:true);
        Assert.Null(result);

        // send one signal
        await context.Client.Entities.SignalEntityAsync(entityId, "Set", 1);

        // wait for state 
        int state = await context.WaitForEntityStateAsync<int>(entityId);
        Assert.Equal(1, state);

        // entity still exists
        result = await context.Client.Entities.GetEntityAsync<int>(entityId);

        Assert.NotNull(result);
        Assert.Equal(1,result!.State);
    }
}
