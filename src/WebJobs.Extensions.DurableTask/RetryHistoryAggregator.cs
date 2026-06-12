// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using DurableTask.Core.History;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Walks an orchestration's history at completion time and emits per-instance
    /// retry aggregate attributes (<c>durabletask.retry_attempt_count</c>,
    /// <c>durabletask.retry_max_attempts_reached</c>) on the current diagnostic
    /// <see cref="Activity"/>.
    ///
    /// MUST be called only on terminal-state turns (Completed / Failed / Terminated /
    /// ContinuedAsNew). Calling on every replay turn would produce repeated, partial-history
    /// span attributes that mislead observability pipelines.
    ///
    /// Performance: short-circuits on the first scan when no retry-tagged events are present.
    /// For long-running orchestrations with very large histories, the walk is O(history length).
    /// </summary>
    internal static class RetryHistoryAggregator
    {
        /// <summary>
        /// Compute aggregate metrics from <paramref name="historyEvents"/> and set them
        /// as tags on <paramref name="activity"/>. Returns silently when <paramref name="activity"/>
        /// is null (no current span) or when there are no retry-tagged events in the history.
        /// </summary>
        public static void EmitToActivity(IList<HistoryEvent>? historyEvents, Activity? activity)
        {
            if (activity == null || historyEvents == null || historyEvents.Count == 0)
            {
                return;
            }

            // Performance short-circuit: walk only to find the first retry-tagged event.
            // If none, emit the trivial zero/false pair and return without further work.
            int retryAttemptCount = 0;
            bool retryMaxAttemptsReached = false;
            bool anyTaggedFound = false;

            // First pass: count retries (attempt > 1) and stash the per-scheduled-event
            // {attempt, maxAttempts} so the second pass can join from TaskFailed events.
            // We use a dictionary keyed by EventId since TaskFailed events reference back via
            // TaskScheduledId. For most orchestrations this dictionary is small (a handful of
            // entries at most).
            Dictionary<int, (int Attempt, int MaxAttempts)>? scheduledById = null;

            for (int i = 0; i < historyEvents.Count; i++)
            {
                if (historyEvents[i] is not TaskScheduledEvent scheduled)
                {
                    continue;
                }

                ActivityRetryMetadata? meta = ActivityRetryMetadata.TryParseFromTags(scheduled.Tags);
                if (meta == null)
                {
                    continue;
                }

                anyTaggedFound = true;
                if (meta.Value.Attempt > 1)
                {
                    retryAttemptCount++;
                }

                scheduledById ??= new Dictionary<int, (int, int)>();
                scheduledById[scheduled.EventId] = (meta.Value.Attempt, meta.Value.MaxAttempts);
            }

            if (!anyTaggedFound)
            {
                // No retry-tagged events. Emit the trivial values so consumers can distinguish
                // "no retries happened" from "metric never emitted." Note: this also fires for
                // non-DTS backends that strip Tags at persistence — there's no way to distinguish
                // those from "genuinely no retries" without backend-specific knowledge.
                activity.SetTag(RetryMetadataConstants.SpanAttrRetryAttemptCount, 0);
                activity.SetTag(RetryMetadataConstants.SpanAttrRetryMaxAttemptsReached, false);
                return;
            }

            // Second pass: join each TaskFailed to its originating TaskScheduledEvent via
            // TaskScheduledId, and check whether the corresponding attempt was at the policy ceiling.
            // TaskFailed events do NOT carry dt.retry.* tags themselves — only the scheduling events do.
            for (int i = 0; i < historyEvents.Count && !retryMaxAttemptsReached; i++)
            {
                if (historyEvents[i] is not TaskFailedEvent failed)
                {
                    continue;
                }

                if (scheduledById!.TryGetValue(failed.TaskScheduledId, out (int Attempt, int MaxAttempts) joined)
                    && joined.Attempt == joined.MaxAttempts)
                {
                    retryMaxAttemptsReached = true;
                }
            }

            activity.SetTag(RetryMetadataConstants.SpanAttrRetryAttemptCount, retryAttemptCount);
            activity.SetTag(RetryMetadataConstants.SpanAttrRetryMaxAttemptsReached, retryMaxAttemptsReached);
        }
    }
}
