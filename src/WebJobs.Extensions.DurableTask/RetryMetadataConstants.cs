// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Reserved constants for the native activity retry visibility feature.
    /// Frozen at v1 — these names must not be renamed post-release without a coordinated change
    /// across DTFx core, the Functions extension, the durable-functions JS SDK, and the DTS Dashboard.
    /// See the design at <c>investigations/df-retry-information/design.MD</c>.
    /// </summary>
    internal static class RetryMetadataConstants
    {
        // ---------------------------------------------------------------------
        // History tag keys (produced by DTFx core in OrchestrationContext.ScheduleWithRetry)
        // ---------------------------------------------------------------------
        public const string HistoryTagAttempt = "dt.retry.attempt";
        public const string HistoryTagMaxAttempts = "dt.retry.maxAttempts";

        // ---------------------------------------------------------------------
        // Worker-facing trigger metadata keys (camelCase to match Functions binding conventions)
        // ---------------------------------------------------------------------
        public const string TriggerKeyAttempt = "durabletask.attempt";
        public const string TriggerKeyMaxAttempts = "durabletask.maxAttempts";
        public const string TriggerKeyIsMaxAttempt = "durabletask.isMaxAttempt";

        // ---------------------------------------------------------------------
        // OpenTelemetry span attribute names (snake_case to match OTel semantic conventions)
        // ---------------------------------------------------------------------
        public const string SpanAttrAttempt = "durabletask.attempt";
        public const string SpanAttrMaxAttempts = "durabletask.max_attempts";
        public const string SpanAttrIsMaxAttempt = "durabletask.is_max_attempt";
        public const string SpanAttrRetryAttemptCount = "durabletask.retry_attempt_count";
        public const string SpanAttrRetryMaxAttemptsReached = "durabletask.retry_max_attempts_reached";
    }
}
