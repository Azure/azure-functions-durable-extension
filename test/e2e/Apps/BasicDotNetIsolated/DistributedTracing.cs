// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.using System.Diagnostics;

using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class DistributedTracing
{
    [Function(nameof(DistributedTracing))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string? activityTraceId = await context.CallActivityAsync<string>(nameof(GetDistributedTraceId));

        return activityTraceId;
    }

    [Function(nameof(GetDistributedTraceId))]
    public static string? GetDistributedTraceId([ActivityTrigger] FunctionContext executionContext)
    {
        return Activity.Current?.Id;
    }
}
