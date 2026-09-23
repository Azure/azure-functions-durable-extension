// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// The worker's purge outcome for one tombstone.
    /// </summary>
    public sealed class LargePayloadPurgeResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LargePayloadPurgeResult"/> class.
        /// </summary>
        /// <param name="tombstoneToken">The opaque correlation token echoed from the tombstone.</param>
        /// <param name="disposition">The worker's classification of the purge outcome.</param>
        public LargePayloadPurgeResult(string tombstoneToken, LargePayloadPurgeDisposition disposition)
        {
            this.TombstoneToken = tombstoneToken ?? throw new ArgumentNullException(nameof(tombstoneToken));
            this.Disposition = disposition;
        }

        /// <summary>
        /// Gets the unchanged tombstone correlation token.
        /// </summary>
        public string TombstoneToken { get; }

        /// <summary>
        /// Gets the purge outcome. Validation and retry scheduling belong to the provider.
        /// </summary>
        public LargePayloadPurgeDisposition Disposition { get; }
    }
}
