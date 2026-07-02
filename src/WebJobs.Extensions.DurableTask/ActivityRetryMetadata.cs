// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Per-attempt retry metadata parsed from <c>TaskScheduledEvent.Tags</c> by the extension and
    /// forwarded to the activity worker via trigger metadata + activity span attributes.
    /// </summary>
    internal readonly struct ActivityRetryMetadata
    {
        // Lock-free one-time flag for the "retry tags present but unparseable" diagnostic warning.
        // Set via Interlocked.Exchange so the warning is emitted at most once per process across
        // BOTH the in-proc activity middleware and the out-of-proc activity middleware paths.
        private static int unparseableTagsWarningEmitted;

        public ActivityRetryMetadata(int attempt, int maxAttempts)
        {
            this.Attempt = attempt;
            this.MaxAttempts = maxAttempts;
        }

        public int Attempt { get; }

        public int MaxAttempts { get; }

        public bool IsMaxAttempt => this.Attempt == this.MaxAttempts;

        /// <summary>
        /// Parse retry metadata from <c>TaskScheduledEvent.Tags</c>. Returns <c>null</c> when
        /// either key is missing, not a strict decimal integer, or fails the bounds check
        /// (<c>attempt &gt;= 1 &amp;&amp; maxAttempts &gt;= 1 &amp;&amp; maxAttempts &gt;= attempt</c>).
        /// </summary>
        /// <remarks>
        /// Parsing uses <see cref="NumberStyles.None"/> + <see cref="CultureInfo.InvariantCulture"/>
        /// to lock the contract: no whitespace, no signs, no hex, no scientific notation, ASCII decimal only.
        /// </remarks>
        public static ActivityRetryMetadata? TryParseFromTags(IDictionary<string, string>? tags)
        {
            if (tags == null)
            {
                return null;
            }

            if (!tags.TryGetValue(RetryMetadataConstants.HistoryTagAttempt, out string? attemptRaw) ||
                !tags.TryGetValue(RetryMetadataConstants.HistoryTagMaxAttempts, out string? maxAttemptsRaw))
            {
                return null;
            }

            if (!int.TryParse(attemptRaw, NumberStyles.None, CultureInfo.InvariantCulture, out int attempt) ||
                !int.TryParse(maxAttemptsRaw, NumberStyles.None, CultureInfo.InvariantCulture, out int maxAttempts))
            {
                return null;
            }

            if (attempt < 1 || maxAttempts < 1 || maxAttempts < attempt)
            {
                return null;
            }

            return new ActivityRetryMetadata(attempt, maxAttempts);
        }

        /// <summary>
        /// Returns <c>true</c> the first time it is called process-wide and <c>false</c> thereafter.
        /// Used by both activity-middleware paths (in-proc and out-of-proc) to gate the "retry tags
        /// present but unparseable" diagnostic warning so it fires at most once per worker process.
        /// </summary>
        public static bool TryClaimUnparseableTagsWarning()
        {
            return Interlocked.Exchange(ref unparseableTagsWarningEmitted, 1) == 0;
        }

        /// <summary>
        /// Returns <c>true</c> when the supplied tags dictionary contains at least one of the
        /// well-known retry-metadata keys (<see cref="RetryMetadataConstants.HistoryTagAttempt"/> or
        /// <see cref="RetryMetadataConstants.HistoryTagMaxAttempts"/>). Used to detect partial /
        /// malformed retry tags so the diagnostic warning fires symmetrically when only one of the
        /// two expected keys is present.
        /// </summary>
        public static bool HasAnyRetryTag(IDictionary<string, string>? tags)
        {
            return tags != null
                && (tags.ContainsKey(RetryMetadataConstants.HistoryTagAttempt)
                    || tags.ContainsKey(RetryMetadataConstants.HistoryTagMaxAttempts));
        }
    }
}
