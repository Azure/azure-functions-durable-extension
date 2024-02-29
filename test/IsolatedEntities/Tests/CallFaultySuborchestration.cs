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

class CallFaultySuborchestration : Test
{
    // this is not an entity test... but it's a good place to put this test

    public override async Task RunAsync(TestContext context)
    {
        string orchestrationName = nameof(CallFaultySuborchestrationOrchestration);
        string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(orchestrationName);
        var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("ok", metadata.ReadOutputAs<string>());
    }
}

class CallFaultySuborchestrationOrchestration
{
    readonly ILogger logger;

    public CallFaultySuborchestrationOrchestration(ILogger<CallFaultyEntityOrchestration> logger)
    {
        this.logger = logger;
    }

    [Function(nameof(FaultySuborchestration))]
    public void FaultySuborchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        this.MethodThatThrows();
    }

    void MethodThatThrows()
    {
        throw new Exception("KABOOM");
    }

    [Function(nameof(CallFaultySuborchestrationOrchestration))]
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        try
        {
            await context.CallSubOrchestratorAsync(nameof(FaultySuborchestration));
            throw new Exception("expected suborchestrator to throw exception, but none was thrown");
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
