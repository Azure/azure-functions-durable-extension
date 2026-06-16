// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Per-attempt retry metadata parsed from <c>TaskScheduledEvent.Tags</c> by the extension and
    /// forwarded to the activity worker via trigger metadata + activity span attributes.
    /// </summary>
    internal readonly struct ActivityRetryMetadata
    {
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
        /// (<c>attempt &gt;= 1 &amp;&amp; maxAttempts &gt;= attempt</c>).
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

            if (attempt < 1 || maxAttempts < attempt)
            {
                return null;
            }

            return new ActivityRetryMetadata(attempt, maxAttempts);
        }
    }
}
