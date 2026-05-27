// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Microsoft.Azure.Durable.Tests.E2E;

// Disabled at runtime via the AzureWebJobs.DisabledOrchestration.Disabled app setting
// in local.settings.json. Used by DisabledOrchestrationTests to validate that the app
// keeps working when a disabled orchestration is registered.
public static class DisabledOrchestration
{
    [Function(nameof(DisabledOrchestration))]
    public static Task RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return Task.CompletedTask;
    }
}
