// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Describes an acknowledged orchestration work-item completion, separately from function invocation success.
    /// </summary>
    public sealed class OrchestrationCompletionOutcome
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestrationCompletionOutcome"/> class.
        /// </summary>
        /// <param name="instanceId">The orchestration instance ID.</param>
        /// <param name="executionId">The execution ID, when available.</param>
        /// <param name="functionName">The orchestrator function name, when available.</param>
        /// <param name="completionIdentity">A stable completion identity that does not expose a work-item authorization token.</param>
        /// <param name="runtimeStatus">The acknowledged orchestration status.</param>
        /// <param name="isAuthoritative">Whether the backend explicitly returned its accepted outcome, rather than only acknowledging a submitted request.</param>
        /// <param name="isProviderGeneratedFailure">Whether the provider or backend replaced the proposed completion with a failure.</param>
        /// <param name="failureDetails">Failure details. Provider-generated details must exclude application payloads and storage response bodies.</param>
        /// <param name="reportingReason">A payload-safe reason for a provider-generated failure, or the application failure reason.</param>
        /// <param name="requestSizeBytes">The original serialized request size, when known.</param>
        /// <param name="sizeLimitBytes">The applicable size limit, when known.</param>
        /// <param name="sizeLimitType">The kind of size limit, distinguishing aggregate requests from individual externalized payloads.</param>
        public OrchestrationCompletionOutcome(
            string instanceId,
            string? executionId,
            string? functionName,
            string completionIdentity,
            OrchestrationStatus runtimeStatus,
            bool isAuthoritative,
            bool isProviderGeneratedFailure = false,
            FailureDetails? failureDetails = null,
            string? reportingReason = null,
            int? requestSizeBytes = null,
            long? sizeLimitBytes = null,
            string? sizeLimitType = null)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new ArgumentException("An instance ID is required.", nameof(instanceId));
            }

            if (string.IsNullOrWhiteSpace(completionIdentity))
            {
                throw new ArgumentException("A completion identity is required.", nameof(completionIdentity));
            }

            if (!Enum.IsDefined(typeof(OrchestrationStatus), runtimeStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeStatus));
            }

            if (isProviderGeneratedFailure &&
                (runtimeStatus != OrchestrationStatus.Failed || failureDetails == null || string.IsNullOrWhiteSpace(reportingReason)))
            {
                throw new ArgumentException("A provider-generated failure requires Failed status, failure details, and a safe reporting reason.", nameof(isProviderGeneratedFailure));
            }

            if (requestSizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestSizeBytes));
            }

            if (sizeLimitBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeLimitBytes));
            }

            this.InstanceId = instanceId;
            this.ExecutionId = executionId;
            this.FunctionName = functionName;
            this.CompletionIdentity = completionIdentity;
            this.RuntimeStatus = runtimeStatus;
            this.IsAuthoritative = isAuthoritative;
            this.IsProviderGeneratedFailure = isProviderGeneratedFailure;
            this.FailureDetails = failureDetails;
            this.ReportingReason = reportingReason;
            this.RequestSizeBytes = requestSizeBytes;
            this.SizeLimitBytes = sizeLimitBytes;
            this.SizeLimitType = sizeLimitType;
        }

        /// <summary>Gets the orchestration instance ID.</summary>
        public string InstanceId { get; }

        /// <summary>Gets the execution ID, when available.</summary>
        public string? ExecutionId { get; }

        /// <summary>Gets the orchestrator function name, when available.</summary>
        public string? FunctionName { get; }

        /// <summary>Gets the stable, non-secret completion identity.</summary>
        public string CompletionIdentity { get; }

        /// <summary>Gets the acknowledged orchestration status.</summary>
        public OrchestrationStatus RuntimeStatus { get; }

        /// <summary>Gets a value indicating whether the backend explicitly confirmed the accepted outcome.</summary>
        public bool IsAuthoritative { get; }

        /// <summary>Gets a value indicating whether a provider or backend generated the terminal failure.</summary>
        public bool IsProviderGeneratedFailure { get; }

        /// <summary>Gets failure details, excluding payloads and storage response bodies for provider-generated failures.</summary>
        public FailureDetails? FailureDetails { get; }

        /// <summary>Gets the failure reason suitable for host reporting.</summary>
        public string? ReportingReason { get; }

        /// <summary>Gets the original serialized completion request size, when known.</summary>
        public int? RequestSizeBytes { get; }

        /// <summary>Gets the applicable size limit, when known.</summary>
        public long? SizeLimitBytes { get; }

        /// <summary>Gets the kind of size limit, when known.</summary>
        public string? SizeLimitType { get; }
    }
}
