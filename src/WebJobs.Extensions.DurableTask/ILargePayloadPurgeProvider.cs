// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Optional durability-provider support for large-payload purge settings and tombstones.
    /// Blob deletion and purge orchestration run in the language worker, not in the host.
    /// </summary>
    /// <remarks>
    /// Implementations operate on their bound task hub and connection. Deadlines and cancellation
    /// are forwarded unchanged from the caller. Providers must bound an unspecified deadline
    /// (<see cref="DateTime.MaxValue"/>) internally when their backend requires a finite deadline.
    /// </remarks>
    public interface ILargePayloadPurgeProvider
    {
        /// <summary>
        /// Records the explicit auto-purge setting without starting any purge work.
        /// </summary>
        /// <param name="enabled">Whether large-payload auto-purge is enabled.</param>
        /// <param name="deadlineUtc">The caller's UTC deadline, or <see cref="DateTime.MaxValue"/> if unspecified.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>A task representing completion of the setting update.</returns>
        Task SetLargePayloadAutoPurgeAsync(bool enabled, DateTime deadlineUtc, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a bounded batch of due tombstones whose payloads the worker should purge.
        /// </summary>
        /// <param name="limit">The requested maximum batch size; the provider applies backend limits.</param>
        /// <param name="deadlineUtc">The caller's UTC deadline, or <see cref="DateTime.MaxValue"/> if unspecified.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>The due tombstones, or an empty list when there is no work.</returns>
        Task<IReadOnlyList<LargePayloadPurgeTombstone>> GetLargePayloadsToPurgeAsync(int limit, DateTime deadlineUtc, CancellationToken cancellationToken);

        /// <summary>
        /// Reports worker purge outcomes. The provider owns retry scheduling and tombstone lifetime.
        /// </summary>
        /// <param name="results">The outcomes with their unchanged tombstone tokens.</param>
        /// <param name="deadlineUtc">The caller's UTC deadline, or <see cref="DateTime.MaxValue"/> if unspecified.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>A task representing completion of the report.</returns>
        Task ReportLargePayloadPurgeResultsAsync(IReadOnlyList<LargePayloadPurgeResult> results, DateTime deadlineUtc, CancellationToken cancellationToken);
    }
}
