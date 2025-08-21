// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.using System.Diagnostics;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Durable.Tests.E2E;

public static class TaskTags
{
    [Function(nameof(TaskTags))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(TaskTags));
        logger.LogInformation("Calling activities...");

        string output1 = await context.CallActivityAsync<string>(nameof(ActivityWithTags), "No Tags");
        string output2 = await context.CallActivityAsync<string>(nameof(ActivityWithTags), "With Tags", new TaskOptions{ Tags = new Dictionary<string, string> { { "key1", "value1" } } });

        logger.LogInformation("Activities called.");

        return $"{output1}\n{output2}";
    }

    [Function(nameof(ActivityWithTags))]
    public static string? ActivityWithTags([ActivityTrigger] string input, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("SayHello");
        logger.LogInformation("Echoing {input}.", nameof(ActivityWithTags));

        return $"Echo: {input}";
    }
}
