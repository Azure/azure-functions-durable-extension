// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    internal class TraceActivityConstants
    {
        public const string Client = "client";
        public const string Orchestration = "orchestration";
        public const string Activity = "activity";
        public const string Event = "event";
        public const string Timer = "timer";

        public const string CreateOrchestration = "create_orchestration";
        public const string OrchestrationEvent = "orchestration_event";
    }
}
