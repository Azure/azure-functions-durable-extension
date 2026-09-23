// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// A tombstoned large payload to be purged by the language worker.
    /// </summary>
    public sealed class LargePayloadPurgeTombstone
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LargePayloadPurgeTombstone"/> class.
        /// </summary>
        /// <param name="tombstoneToken">The opaque backend-issued correlation token.</param>
        /// <param name="payloadToken">The unchanged external payload token.</param>
        public LargePayloadPurgeTombstone(string tombstoneToken, string payloadToken)
        {
            this.TombstoneToken = tombstoneToken ?? throw new ArgumentNullException(nameof(tombstoneToken));
            this.PayloadToken = payloadToken ?? throw new ArgumentNullException(nameof(payloadToken));
        }

        /// <summary>
        /// Gets the opaque correlation token that must be echoed unchanged in the purge result.
        /// </summary>
        public string TombstoneToken { get; }

        /// <summary>
        /// Gets the external payload token, which the host must not interpret or normalize.
        /// </summary>
        public string PayloadToken { get; }
    }
}
