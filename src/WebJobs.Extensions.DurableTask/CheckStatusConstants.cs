// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal static class CheckStatusConstants
    {
        internal const string InstancesControllerSegment = "/instances/";
        internal const string TaskHubParameter = "taskHub";
        internal const string ConnectionParameter = "connection";
        internal const string RaiseEventOperation = "raiseEvent";
        internal const string TerminateOperation = "terminate";
    }
}
