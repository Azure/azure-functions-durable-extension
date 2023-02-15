// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    internal enum FunctionState
    {
        Scheduled,
        Started,
        Awaited,
        Listening,
        Completed,
        Terminated,
        Failed,
        ExternalEventRaised,
        ExternalEventDropped,
        LockAcquired,
        LockReleased,
        TimerExpired,
        Rewound,
        EntityStateCreated,
        EntityStateDeleted,
        Suspended,
        EntityBatch,
    }
}
