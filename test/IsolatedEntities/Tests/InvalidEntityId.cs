// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace IsolatedEntities;

/// <summary>
/// This test is not entity related, but discovered an issue with how failures in orchestrators are captured.
/// </summary>
class InvalidEntityId : Test
{
    public enum Location
    {
        ClientSignal,
        ClientGet,
        OrchestrationSignal,
        OrchestrationCall,
    }

    readonly Location location;

    public InvalidEntityId(Location location)
    {
        this.location = location;
    }

    public override string Name => $"{base.Name}.{this.location}";

    public override async Task RunAsync(TestContext context)
    {
        switch (this.location)
        {
            case Location.ClientSignal:
                await Assert.ThrowsAsync(
                    typeof(ArgumentNullException),
                    async () =>
                    {
                        await context.Client.Entities.SignalEntityAsync(default, "add", 1);
                    });
                return;

            case Location.ClientGet:
                await Assert.ThrowsAsync(
                    typeof(ArgumentNullException),
                    async () =>
                    {
                        await context.Client.Entities.GetEntityAsync(default);
                    });
                return;

            case Location.OrchestrationSignal:
                {
                    string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(nameof(SignalAndCall.SignalAndCallOrchestration) /* missing input */);
                    var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);
                    Assert.Equal(OrchestrationRuntimeStatus.Failed, metadata.RuntimeStatus);
                    //Assert.NotNull(metadata.FailureDetails);  // TODO currently failing because FailureDetails are not propagated for some reason
                }
                break;

            case Location.OrchestrationCall:
                {
                    string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(nameof(CallCounter.CallCounterOrchestration) /* missing input */);
                    var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);
                    Assert.Equal(OrchestrationRuntimeStatus.Failed, metadata.RuntimeStatus);
                    //Assert.NotNull(metadata.FailureDetails);  // TODO currently failing because FailureDetails are not propagated for some reason
                }
                break;
        }
    }
}
