// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration options for the Durable Task extension.
    /// </summary>
    public class DurableTaskOptions
    {
        /// <summary>
        /// The default task hub name to use when not explicitly configured.
        /// </summary>
        internal const string DefaultHubName = "DurableFunctionsHub";

        /// <summary>
        /// Gets or sets default task hub name to be used by all <see cref="DurableOrchestrationClient"/>,
        /// <see cref="DurableOrchestrationContext"/>, and <see cref="DurableActivityContext"/> instances.
        /// </summary>
        /// <remarks>
        /// A task hub is a logical grouping of storage resources. Alternate task hub names can be used to isolate
        /// multiple Durable Functions applications from each other, even if they are using the same storage backend.
        /// </remarks>
        /// <value>The name of the default task hub.</value>
        public string HubName { get; set; } = DefaultHubName;

        /// <summary>
        /// Gets or sets the number of messages to pull from the control queue at a time.
        /// </summary>
        /// <remarks>
        /// Messages pulled from the control queue are buffered in memory until the internal
        /// dispatcher is ready to process them.
        /// </remarks>
        /// <value>A positive integer configured by the host. The default value is <c>32</c>.</value>
        public int ControlQueueBatchSize { get; set; } = 32;

        /// <summary>
        /// Gets or sets the partition count for the control queue.
        /// </summary>
        /// <remarks>
        /// Increasing the number of partitions will increase the number of workers
        /// that can concurrently execute orchestrator functions. However, increasing
        /// the partition count can also increase the amount of load placed on the storage
        /// account and on the thread pool if the number of workers is smaller than the
        /// number of partitions.
        /// </remarks>
        /// <value>A positive integer between 1 and 16. The default value is <c>4</c>.</value>
        public int PartitionCount { get; set; } = 4;

        /// <summary>
        /// Gets or sets the visibility timeout of dequeued control queue messages.
        /// </summary>
        /// <value>
        /// A <c>TimeSpan</c> configured by the host. The default is 5 minutes.
        /// </value>
        public TimeSpan ControlQueueVisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the visibility timeout of dequeued work item queue messages.
        /// </summary>
        /// <value>
        /// A <c>TimeSpan</c> configured by the host. The default is 5 minutes.
        /// </value>
        public TimeSpan WorkItemQueueVisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

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
        /// Gets or sets the name of the Azure Storage connection string used to manage the underlying Azure Storage resources.
        /// </summary>
        /// <remarks>
        /// If not specified, the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string AzureStorageConnectionStringName { get; set; }

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
        /// Gets or sets a value indicating whether to trace the inputs and outputs of function calls.
        /// </summary>
        /// <remarks>
        /// The default behavior when tracing function execution events is to include the number of bytes in the serialized
        /// inputs and outputs for function calls. This provides minimal information about what the inputs and outputs look
        /// like without bloating the logs or inadvertently exposing sensitive information to the logs. Setting
        /// <see cref="TraceInputsAndOutputs"/> to <c>true</c> will instead cause the default function logging to log
        /// the entire contents of function inputs and outputs.
        /// </remarks>
        /// <value>
        /// <c>true</c> to trace the raw values of inputs and outputs; otherwise <c>false</c>.
        /// </value>
        public bool TraceInputsAndOutputs { get; set; }

        /// <summary>
        /// Gets or sets the URL of an Azure Event Grid custom topic endpoint.
        /// When set, orchestration life cycle notification events will be automatically
        /// published to this endpoint.
        /// </summary>
        /// <remarks>
        /// Azure Event Grid topic URLs are generally expected to be in the form
        /// https://{topic_name}.{region}.eventgrid.azure.net/api/events.
        /// </remarks>
        /// <value>
        /// The Azure Event Grid custom topic URL.
        /// </value>
        public string EventGridTopicEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the name of the app setting containing the key used for authenticating with the Azure Event Grid custom topic at <see cref="EventGridTopicEndpoint"/>.
        /// </summary>
        /// <value>
        /// The name of the app setting that stores the Azure Event Grid key.
        /// </value>
        public string EventGridKeySettingName { get; set; }

        /// <summary>
        /// Gets or sets the Event Grid publish request retry count.
        /// </summary>
        /// <value>The number of retry attempts.</value>
        public int EventGridPublishRetryCount { get; set; }

        /// <summary>
        /// Gets orsets the Event Grid publish request retry interval.
        /// </summary>
        /// <value>A <see cref="TimeSpan"/> representing the retry interval. The default value is 5 minutes.</value>
        public TimeSpan EventGridPublishRetryInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the Event Grid publish request http status.
        /// </summary>
        /// <value>A list of HTTP status codes, e.g. 400, 403.</value>
        public int[] EventGridPublishRetryHttpStatus { get; set; }

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
        /// Gets or sets if logs for replay events need to be recorded.
        /// </summary>
        /// <remarks>
        /// The default value is false, which disables the logging of replay events.
        /// </remarks>
        /// <value>
        /// Boolean value specifying if the replay events should be logged.
        /// </value>
        public bool LogReplayEvents { get; set; }

        // Used for mocking the lifecycle notification helper.
        internal HttpMessageHandler NotificationHandler { get; set; }

        internal string GetDebugString()
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("Initializing extension with the following settings:");
            sb.Append(nameof(this.AzureStorageConnectionStringName)).Append(": ").Append(this.AzureStorageConnectionStringName).Append(", ");
            sb.Append(nameof(this.MaxConcurrentActivityFunctions)).Append(": ").Append(this.MaxConcurrentActivityFunctions).Append(", ");
            sb.Append(nameof(this.MaxConcurrentOrchestratorFunctions)).Append(": ").Append(this.MaxConcurrentOrchestratorFunctions).Append(", ");
            sb.Append(nameof(this.PartitionCount)).Append(": ").Append(this.PartitionCount).Append(", ");
            sb.Append(nameof(this.ControlQueueBatchSize)).Append(": ").Append(this.ControlQueueBatchSize).Append(", ");
            sb.Append(nameof(this.ControlQueueVisibilityTimeout)).Append(": ").Append(this.ControlQueueVisibilityTimeout).Append(", ");
            sb.Append(nameof(this.WorkItemQueueVisibilityTimeout)).Append(": ").Append(this.WorkItemQueueVisibilityTimeout).Append(", ");

            sb.Append(nameof(this.ExtendedSessionsEnabled)).Append(": ").Append(this.ExtendedSessionsEnabled).Append(", ");
            if (this.ExtendedSessionsEnabled)
            {
                sb.Append(nameof(this.ExtendedSessionIdleTimeoutInSeconds)).Append(": ").Append(this.ExtendedSessionIdleTimeoutInSeconds).Append(", ");
            }

            sb.Append(nameof(this.EventGridTopicEndpoint)).Append(": ").Append(this.EventGridTopicEndpoint).Append(", ");
            if (!string.IsNullOrEmpty(this.EventGridTopicEndpoint))
            {
                sb.Append(nameof(this.EventGridKeySettingName)).Append(": ").Append(this.EventGridKeySettingName).Append(", ");
                sb.Append(nameof(this.EventGridPublishRetryCount)).Append(": ").Append(this.EventGridPublishRetryCount).Append(", ");
                sb.Append(nameof(this.EventGridPublishRetryInterval)).Append(": ").Append(this.EventGridPublishRetryInterval).Append(", ");
                sb.Append(nameof(this.EventGridPublishRetryHttpStatus)).Append(": ").Append(string.Join(", ", this.EventGridPublishRetryHttpStatus ?? new int[0])).Append(", ");
            }

            if (this.NotificationUrl != null)
            {
                // Don't trace the query string, since that contains secrets
                string url = this.NotificationUrl.GetLeftPart(UriPartial.Path);
                sb.Append(nameof(this.NotificationUrl)).Append(": ").Append(url).Append(", ");
            }

            sb.Append(nameof(this.LogReplayEvents)).Append(": ").Append(this.LogReplayEvents);

            return sb.ToString();
        }

        internal void Validate()
        {
            if (string.IsNullOrEmpty(this.HubName))
            {
                throw new InvalidOperationException($"A non-empty {nameof(this.HubName)} configuration is required.");
            }

            try
            {
                NameValidator.ValidateBlobName(this.HubName);
                NameValidator.ValidateContainerName(this.HubName.ToLowerInvariant());
                NameValidator.ValidateTableName(this.HubName);
                NameValidator.ValidateQueueName(this.HubName.ToLowerInvariant());
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(
                    $"Task hub name '{this.HubName}' should contain only alphanumeric characters excluding '-' and have length up to 50.", e);
            }

            if (this.ControlQueueBatchSize <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.ControlQueueBatchSize)} must be a non-negative integer.");
            }

            if (this.PartitionCount < 1 || this.PartitionCount > 16)
            {
                throw new InvalidOperationException($"{nameof(this.PartitionCount)} must be an integer value between 1 and 16.");
            }

            if (this.ControlQueueVisibilityTimeout < TimeSpan.FromMinutes(1) ||
                this.ControlQueueVisibilityTimeout > TimeSpan.FromMinutes(60))
            {
                throw new InvalidOperationException($"{nameof(this.ControlQueueVisibilityTimeout)} must be between 1 and 60 minutes.");
            }

            if (this.MaxConcurrentActivityFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentActivityFunctions)} must be a non-negative integer value.");
            }

            if (this.MaxConcurrentOrchestratorFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentOrchestratorFunctions)} must be a non-negative integer value.");
            }

            if (this.EventGridPublishRetryInterval <= TimeSpan.Zero ||
                this.EventGridPublishRetryInterval > TimeSpan.FromMinutes(60))
            {
                throw new InvalidOperationException($"{nameof(this.EventGridPublishRetryInterval)} must be non-negative and no more than 60 minutes.");
            }
        }
    }
}
