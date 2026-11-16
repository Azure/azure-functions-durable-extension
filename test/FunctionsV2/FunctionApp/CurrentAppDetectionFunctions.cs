// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal static class CurrentAppDetectionFunctions
    {
        public static string TargetOnlyOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            return $"target:{context.GetInput<string>()}";
        }
    }
}
