﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration options for the Durable Task extension.
    /// </summary>
    public class DurableTaskOptions
    {
        /// <summary>
        /// Gets or sets default task hub name to be used by all <see cref="DurableOrchestrationClient"/>,
        /// <see cref="DurableOrchestrationContext"/>, and <see cref="DurableActivityContext"/> instances.
        /// </summary>
        /// <remarks>
        /// A task hub is a logical grouping of storage resources. Alternate task hub names can be used to isolate
        /// multiple Durable Functions applications from each other, even if they are using the same storage backend.
        /// </remarks>
        /// <value>The name of the default task hub.</value>
        public string HubName { get; set; }

        /// <summary>
        /// The section of configuration related to storage providers.
        /// </summary>
        public StorageProviderOptions StorageProvider { get; set; }

        /// <summary>
        /// The section of configuration related to tracing.
        /// </summary>
        public TraceOptions Tracing { get; set; } = new TraceOptions();

        /// <summary>
        /// The section of configuration related to notifications.
        /// </summary>
        public NotificationOptions Notifications { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activity functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <remarks>
        /// Increasing activity function concurrent can result in increased throughput but can
        /// also increase the total CPU and memory usage on a single worker instance.
        /// </remarks>
        /// <value>
        /// A positive integer configured by the host. The default value is 10X the number of processors on the current machine.
        /// </value>
        public int MaxConcurrentActivityFunctions { get; set; } = 10 * Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the maximum number of orchestrator functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <value>
        /// A positive integer configured by the host. The default value is 10X the number of processors on the current machine.
        /// </value>
        public int MaxConcurrentOrchestratorFunctions { get; set; } = 10 * Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets a flag indicating whether to enable extended sessions.
        /// </summary>
        /// <remarks>
        /// <para>Extended sessions can improve the performance of orchestrator functions by allowing them to skip
        /// replays when new messages are received within short periods of time.</para>
        /// <para>Note that orchestrator functions which are extended this way will continue to count against the
        /// <see cref="MaxConcurrentOrchestratorFunctions"/> limit. To avoid starvation, only half of the maximum
        /// number of allowed concurrent orchestrator functions can be concurrently extended at any given time.
        /// The <see cref="ExtendedSessionIdleTimeoutInSeconds"/> property can also be used to control how long an idle
        /// orchestrator function is allowed to be extended.</para>
        /// <para>It is recommended that this property be set to <c>false</c> during development to help
        /// ensure that the orchestrator code correctly obeys the idempotency rules.</para>
        /// </remarks>
        /// <value>
        /// <c>true</c> to enable extended sessions; otherwise <c>false</c>.
        /// </value>
        public bool ExtendedSessionsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the amount of time in seconds before an idle session times out. The default value is 30 seconds.
        /// </summary>
        /// <remarks>
        /// This setting is applicable when <see cref="ExtendedSessionsEnabled"/> is set to <c>true</c>.
        /// </remarks>
        /// <value>
        /// The number of seconds before an idle session times out.
        /// </value>
        public int ExtendedSessionIdleTimeoutInSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the type name of a custom to use for handling lifecycle notification events.
        /// </summary>
        /// <value>Assembly qualified class name that implements <see cref="ILifeCycleNotificationHelper">ILifeCycleNotificationHelper</see>.</value>
        public string CustomLifeCycleNotificationHelperType { get; set; }

        // Used for mocking the lifecycle notification helper.
        internal HttpMessageHandler NotificationHandler { get; set; }

        internal string GetDebugString()
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("Initializing extension with the following settings:");
            sb.Append(nameof(this.HubName)).Append(":").Append(this.HubName).Append(", ");

            sb.Append(nameof(this.StorageProvider)).Append(": { ");
            this.StorageProvider.AddToDebugString(sb);
            sb.Append(" }, ");

            sb.Append(nameof(this.MaxConcurrentActivityFunctions)).Append(": ").Append(this.MaxConcurrentActivityFunctions).Append(", ");
            sb.Append(nameof(this.MaxConcurrentOrchestratorFunctions)).Append(": ").Append(this.MaxConcurrentOrchestratorFunctions).Append(", ");
            sb.Append(nameof(this.ExtendedSessionsEnabled)).Append(": ").Append(this.ExtendedSessionsEnabled).Append(", ");
            if (this.ExtendedSessionsEnabled)
            {
                sb.Append(nameof(this.ExtendedSessionIdleTimeoutInSeconds)).Append(": ").Append(this.ExtendedSessionIdleTimeoutInSeconds).Append(", ");
            }

            if (this.Notifications != null)
            {
                this.Notifications.AddToDebugString(sb);
            }

            if (this.Tracing != null)
            {
                this.Tracing.AddToDebugString(sb);
            }

            return sb.ToString();
        }

        /// <summary>
        /// A helper method to help retrieve the connection string name for the configured storage provider.
        /// </summary>
        /// <returns>The connection string name for the configured storage provider.</returns>
        public string GetConnectionStringName()
        {
            return this.StorageProvider.GetConfiguredProvider().ConnectionStringName;
        }

        internal void Validate()
        {
            if (string.IsNullOrEmpty(this.HubName))
            {
                throw new InvalidOperationException($"A non-empty {nameof(this.HubName)} configuration is required.");
            }

            this.StorageProvider.Validate();

            // Each storage provider may have its own limitations for task hub names due to provider naming restrictions
            this.StorageProvider.GetConfiguredProvider().ValidateHubName(this.HubName);

            if (this.Notifications != null)
            {
                this.Notifications.Validate();
            }

            if (this.MaxConcurrentActivityFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentActivityFunctions)} must be a non-negative integer value.");
            }

            if (this.MaxConcurrentOrchestratorFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentOrchestratorFunctions)} must be a non-negative integer value.");
            }
        }
    }
}
