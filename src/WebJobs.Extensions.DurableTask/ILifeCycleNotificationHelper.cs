// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Interface defining methods to life cycle notifications.
    /// </summary>
    public interface ILifeCycleNotificationHelper
    {
        /// <summary>
        /// The orchestrator was starting.
        /// </summary>
        /// <param name="hubName">The name of the task hub.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="instanceId">The ID to use for the orchestration instance.</param>
        /// <param name="isReplay">The orchestrator function is currently replaying itself.</param>
        /// <returns>A task that completes when the lifecycle notification message has been sent.</returns>
        Task OrchestratorStartingAsync(string hubName, string functionName, string instanceId, bool isReplay);

        /// <summary>
        /// The orchestrator was completed.
        /// </summary>
        /// <param name="hubName">The name of the task hub.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="instanceId">The ID to use for the orchestration instance.</param>
        /// <param name="continuedAsNew">The orchestration completed with ContinueAsNew as is in the process of restarting.</param>
        /// <param name="isReplay">The orchestrator function is currently replaying itself.</param>
        /// <returns>A task that completes when the lifecycle notification message has been sent.</returns>
        Task OrchestratorCompletedAsync(string hubName, string functionName, string instanceId, bool continuedAsNew, bool isReplay);

        /// <summary>
        /// The orchestrator was failed.
        /// </summary>
        /// <param name="hubName">The name of the task hub.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="instanceId">The ID to use for the orchestration instance.</param>
        /// <param name="reason">Additional data associated with the tracking event.</param>
        /// <param name="isReplay">The orchestrator function is currently replaying itself.</param>
        /// <returns>A task that completes when the lifecycle notification message has been sent.</returns>
        Task OrchestratorFailedAsync(string hubName, string functionName, string instanceId, string reason, bool isReplay);

        /// <summary>
        /// The orchestrator was terminated.
        /// </summary>
        /// <param name="hubName">The name of the task hub.</param>
        /// <param name="functionName">The name of the orchestrator function to call.</param>
        /// <param name="instanceId">The ID to use for the orchestration instance.</param>
        /// <param name="reason">Additional data associated with the tracking event.</param>
        /// <returns>A task that completes when the lifecycle notification message has been sent.</returns>
        Task OrchestratorTerminatedAsync(string hubName, string functionName, string instanceId, string reason);
    }
}