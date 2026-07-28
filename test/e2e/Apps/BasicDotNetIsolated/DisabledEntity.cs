// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Entities;

namespace Microsoft.Azure.Durable.Tests.E2E;

// Disabled at runtime via the AzureWebJobs.DisabledEntity.Disabled app setting
// in local.settings.json. Used by DisabledOrchestrationTests to validate that the app
// keeps working when a disabled entity is registered.
public static class DisabledEntity
{
    [Function(nameof(DisabledEntity))]
    public static Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
        => dispatcher.DispatchAsync<object>();
}
