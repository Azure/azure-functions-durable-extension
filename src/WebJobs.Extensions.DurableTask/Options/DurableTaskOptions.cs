// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration options for the Durable Task extension.
    /// </summary>
    public class DurableTaskOptions
    {
        private string hubName;

        private string defaultHubName;

        /// <summary>
        /// Settings used for Durable HTTP functionality.
        /// </summary>
        public HttpOptions HttpSettings { get; set; }

        /// <summary>
        /// Gets or sets default task hub name to be used by all <see cref="IDurableClient"/>, <see cref="IDurableEntityClient"/>, <see cref="IDurableOrchestrationClient"/>,
        /// <see cref="IDurableOrchestrationContext"/>, and <see cref="IDurableActivityContext"/> instances.
        /// </summary>
        /// <remarks>
        /// A task hub is a logical grouping of storage resources. Alternate task hub names can be used to isolate
        /// multiple Durable Functions applications from each other, even if they are using the same storage backend.
        /// </remarks>
        /// <value>The name of the default task hub.</value>
        public string HubName
        {
            get
            {
                if (this.hubName == null)
                {
                    // "WEBSITE_SITE_NAME" is an environment variable used in Azure functions infrastructure. When running locally, this can be
                    // specified in local.settings.json file to avoid being defaulted to "TestHubName"
                    this.hubName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "TestHubName";
                    this.defaultHubName = this.hubName;
                }

                return this.hubName;
            }

            set
            {
                this.hubName = value;
            }
        }

        /// <summary>
        /// The section of configuration related to storage providers. If using Azure Storage provider, the schema should match
        /// <see cref="AzureStorageOptions"/>.
        /// </summary>
        public IDictionary<string, object> StorageProvider { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// The section of configuration related to tracing.
        /// </summary>
        public TraceOptions Tracing { get; set; } = new TraceOptions();

        /// <summary>
        /// The section of configuration related to notifications.
        /// </summary>
        public NotificationOptions Notifications { get; set; } = new NotificationOptions();

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
        /// Gets or sets the base URL for the HTTP APIs managed by this extension.
        /// </summary>
        /// <remarks>
        /// This property is intended for use only by runtime hosts.
        /// </remarks>
        /// <value>
        /// A URL pointing to the hosted function app that responds to status polling requests.
        /// </value>
        public Uri NotificationUrl { get; set; }

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
        /// Gets or sets the maximum number of orchestration actions. The default value is 100,000.
        /// </summary>
        public int MaxOrchestrationActions { get; set; } = 100000;

        /// <summary>
        ///  States that will override an existing orchestrator when attempting to start a new orchestrator with the same instance Id.
        /// </summary>
        public OverridableStates OverridableExistingInstanceStates { get; set; } = OverridableStates.NonRunningStates;

        /// <summary>
        /// Gets or sets the time window within which entity messages get deduplicated and reordered.
        /// </summary>
        public int EntityMessageReorderWindowInMinutes { get; set; } = 30;

        // Used for mocking the lifecycle notification helper.
        internal HttpMessageHandler NotificationHandler { get; set; }

        /// <summary>
        /// Sets HubName to a value that is considered a default value.
        /// </summary>
        /// <param name="hubName">TaskHub name that is considered the default.</param>
        public void SetDefaultHubName(string hubName)
        {
            this.HubName = hubName;
            this.defaultHubName = hubName;
        }

        internal string GetDebugString()
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("Initializing extension with the following settings:");
            sb.Append(nameof(this.HubName)).Append(":").Append(this.HubName).Append(", ");

            this.AppendStorageProviderValuesToDebugString(sb);

            sb.Append(nameof(this.MaxConcurrentActivityFunctions)).Append(": ").Append(this.MaxConcurrentActivityFunctions).Append(", ");
            sb.Append(nameof(this.MaxConcurrentOrchestratorFunctions)).Append(": ").Append(this.MaxConcurrentOrchestratorFunctions).Append(", ");
            sb.Append(nameof(this.ExtendedSessionsEnabled)).Append(": ").Append(this.ExtendedSessionsEnabled).Append(", ");
            if (this.ExtendedSessionsEnabled)
            {
                sb.Append(nameof(this.ExtendedSessionIdleTimeoutInSeconds)).Append(": ").Append(this.ExtendedSessionIdleTimeoutInSeconds).Append(", ");
            }

            if (this.NotificationUrl != null)
            {
                // Don't trace the query string, since that contains secrets
                string url = this.NotificationUrl.GetLeftPart(UriPartial.Path);
                sb.Append(nameof(this.NotificationUrl)).Append(": ").Append(url).Append(", ");
            }

            sb.Append(nameof(this.Notifications)).Append(": { ");
            this.Notifications.AddToDebugString(sb);
            sb.Append(" }, ");

            sb.Append(nameof(this.Tracing)).Append(": { ");
            this.Tracing.AddToDebugString(sb);
            sb.Append(" }");

            return sb.ToString();
        }

        private void AppendStorageProviderValuesToDebugString(StringBuilder sb)
        {
            sb.Append(nameof(this.StorageProvider)).Append(": { ");
            foreach (var value in this.StorageProvider)
            {
                sb.Append(value.Key).Append(": ").Append(value.Value).Append(", ");
            }

            sb.Append(" }, ");
        }

        internal void Validate()
        {
            if (string.IsNullOrEmpty(this.HubName))
            {
                throw new InvalidOperationException($"A non-empty {nameof(this.HubName)} configuration is required.");
            }

            if (IsInNonProductionSlot() && this.IsDefaultHubName())
            {
                throw new InvalidOperationException($"Task Hub name must be specified in host.json when using slots. Specified name must not equal the default HubName ({this.defaultHubName})." +
                    "See documentation on Task Hubs for information on how to set this: https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-task-hubs");
            }

            this.Notifications.Validate();

            if (this.MaxConcurrentActivityFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentActivityFunctions)} must be a non-negative integer value.");
            }

            if (this.MaxConcurrentOrchestratorFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentOrchestratorFunctions)} must be a non-negative integer value.");
            }
        }

        internal bool IsDefaultHubName()
        {
            return string.Equals(this.defaultHubName, this.hubName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInNonProductionSlot()
        {
            var slotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            // slotName can be null in a test environment
            if (slotName != null && !string.Equals(slotName, "Production", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
