// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Optional capability for a <see cref="DurabilityProvider"/> that reports acknowledged orchestration completion outcomes.
    /// </summary>
    public interface IOrchestrationCompletionOutcomeProvider
    {
        /// <summary>
        /// Completes a work item and reports its acknowledged outcome. Submission errors must propagate as exceptions.
        /// </summary>
        /// <param name="workItem">The work item being completed.</param>
        /// <param name="newOrchestrationRuntimeState">The proposed runtime state.</param>
        /// <param name="outboundMessages">The proposed activity messages.</param>
        /// <param name="orchestratorMessages">The proposed orchestration messages.</param>
        /// <param name="timerMessages">The proposed timer messages.</param>
        /// <param name="continuedAsNewMessage">The proposed continue-as-new message, if any.</param>
        /// <param name="orchestrationState">The proposed persisted orchestration state, if available.</param>
        /// <returns>
        /// The outcome after backend acknowledgement, or null for an entity work item that has no orchestration outcome.
        /// Older backends may return a non-authoritative outcome based on the acknowledged submitted request.
        /// </returns>
        /// <remarks>
        /// Return an outcome only after receiving a valid acknowledgement. Set
        /// <see cref="OrchestrationCompletionOutcome.IsAuthoritative"/> only when the backend explicitly reports
        /// its accepted outcome, including any backend-generated replacement. Logging a rejection is not an acknowledgement.
        /// The extension uses this result for best-effort post-acknowledgement reporting, not durable notification delivery.
        /// </remarks>
        Task<OrchestrationCompletionOutcome?> CompleteTaskOrchestrationWorkItemWithOutcomeAsync(
            TaskOrchestrationWorkItem workItem,
            OrchestrationRuntimeState newOrchestrationRuntimeState,
            IList<TaskMessage> outboundMessages,
            IList<TaskMessage> orchestratorMessages,
            IList<TaskMessage> timerMessages,
            TaskMessage? continuedAsNewMessage,
            OrchestrationState? orchestrationState);
    }
}
