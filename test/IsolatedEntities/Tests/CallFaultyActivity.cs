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

class CallFaultyActivity : Test
{
    // this is not an entity test... but it's a good place to put this test

    public override async Task RunAsync(TestContext context)
    {
        string orchestrationName = nameof(CallFaultyActivityOrchestration);
        string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(orchestrationName);
        var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("ok", metadata.ReadOutputAs<string>());
    }
}

class CallFaultyActivityOrchestration
{
    readonly ILogger logger;

    public CallFaultyActivityOrchestration(ILogger<CallFaultyEntityOrchestration> logger)
    {
        this.logger = logger;
    }

    [Function(nameof(FaultyActivity))]
    public void FaultyActivity([ActivityTrigger] TaskActivityContext context)
    {
        this.MethodThatThrows();
    }

    void MethodThatThrows()
    {
        throw new Exception("KABOOM");
    }

    [Function(nameof(CallFaultyActivityOrchestration))]
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        try
        {
            await context.CallActivityAsync(nameof(FaultyActivity));
            throw new Exception("expected activity to throw exception, but none was thrown");
        }
        catch (TaskFailedException taskFailedException)
        {
            Assert.NotNull(taskFailedException.FailureDetails);
            Assert.Equal("KABOOM", taskFailedException.FailureDetails.ErrorMessage);
            Assert.Contains(nameof(MethodThatThrows), taskFailedException.FailureDetails.StackTrace);
        }
        catch (Exception e)
        {
            throw new Exception($"wrong exception thrown", e);
        }

        return "ok";
    }
}
