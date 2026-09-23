// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The outcome of a worker's attempt to purge a tombstoned large payload.
    /// </summary>
    public enum LargePayloadPurgeDisposition
    {
        /// <summary>
        /// No outcome was specified. The backend must reject this value, not treat it as success.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Terminal success: the payload was deleted, was already absent, or is not owned by the payload store.
        /// </summary>
        Deleted = 1,

        /// <summary>
        /// A potentially transient failure. The backend schedules another attempt.
        /// </summary>
        Retry = 2,

        /// <summary>
        /// A deterministic failure. Preserve the tombstone for operator action without retrying it.
        /// </summary>
        Quarantined = 3,
    }
}
