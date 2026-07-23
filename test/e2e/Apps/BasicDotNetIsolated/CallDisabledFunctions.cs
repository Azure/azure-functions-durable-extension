// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;

namespace Microsoft.Azure.Durable.Tests.E2E;

// Orchestrations that schedule work against the disabled-but-still-deployed functions
// (DisabledActivity / DisabledEntity). These are used by DisabledOrchestrationTests to verify that
// dispatching a disabled function fails the orchestration deterministically instead of poison-looping
// forever. See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
public static class CallDisabledFunctions
{
    [Function(nameof(CallDisabledActivity))]
    public static async Task<string> CallDisabledActivity(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // DisabledActivity is indexed but has no active listener (its executor is null), so this
        // must surface as a deterministic activity failure rather than hanging forever.
        return await context.CallActivityAsync<string>(nameof(DisabledActivity), "hello");
    }

    [Function(nameof(CallDisabledEntity))]
    public static async Task<string> CallDisabledEntity(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var entityId = new EntityInstanceId(nameof(DisabledEntity), "disabled-key");

        // DisabledEntity is indexed but has no active listener, so calling an operation on it must
        // fail deterministically rather than poison-looping the entity work item forever.
        await context.Entities.CallEntityAsync(entityId, "someOperation");
        return "should not reach here";
    }
}
