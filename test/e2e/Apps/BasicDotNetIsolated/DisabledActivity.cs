// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.Durable.Tests.E2E;

// Disabled at runtime via the AzureWebJobs.DisabledActivity.Disabled app setting
// in local.settings.json. Used by DisabledOrchestrationTests to validate that the app
// keeps working when a disabled activity is registered.
public static class DisabledActivity
{
    [Function(nameof(DisabledActivity))]
    public static string Run([ActivityTrigger] string input) => input;
}
