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
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsolatedEntities;

class SelfScheduling : Test
{
    public override async Task RunAsync(TestContext context)
    {
        var entityId = new EntityInstanceId(nameof(SelfSchedulingEntity), Guid.NewGuid().ToString().Substring(0,8));

        await context.Client.Entities.SignalEntityAsync(entityId, "start");

        var result = await context.WaitForEntityStateAsync<SelfSchedulingEntity>(
            entityId,
            timeout: default,
            entityState => entityState.Value.Length == 4 ? null : "expect 4 letters");

        Assert.NotNull(result);
        Assert.Equal("ABCD", result.Value);
    }
}
