// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the Azure Storage storage provider.
    /// </summary>
    public class AzureStorageOptions : CommonStorageProviderOptions
    {
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
        /// Gets or sets the name of the Azure Storage connection string to use for the
        /// durable tracking store (History and Instances tables).
        /// </summary>
        /// <remarks><para>
        /// If not specified, the <see cref="CommonStorageProviderOptions.ConnectionStringName"/> connection string
        /// is used for the durable tracking store.
        /// </para><para>
        /// This property is primarily useful when deploying multiple apps that need to share the same
        /// tracking infrastructure. For example, when deploying two versions of an app side by side, using
        /// the same tracking store allows both versions to save history into the same table, which allows
        /// clients to query for instance status across all versions.
        /// </para></remarks>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string TrackingStoreConnectionStringName { get; set; }

        /// <summary>
        /// Gets or sets the name prefix to use for history and instance tables in Azure Storage.
        /// </summary>
        /// <remarks>
        /// This property is only used when <see cref="TrackingStoreConnectionStringName"/> is specified.
        /// If no prefix is specified, the default prefix value is "DurableTask".
        /// </remarks>
        /// <value>The prefix to use when naming the generated Azure tables.</value>
        public string TrackingStoreNamePrefix { get; set; }

        /// <summary>
        /// Gets or sets the maximum queue polling interval.
        /// </summary>
        /// <value>Maximum interval for polling control and work-item queues.</value>
        public TimeSpan MaxQueuePollingInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal override void ValidateHubName(string hubName)
        {
            try
            {
                NameValidator.ValidateBlobName(hubName);
                NameValidator.ValidateContainerName(hubName.ToLowerInvariant());
                NameValidator.ValidateTableName(hubName);
                NameValidator.ValidateQueueName(hubName.ToLowerInvariant());
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(
                    $"Task hub name '{hubName}' should contain only alphanumeric characters excluding '-' and have length up to 50.", e);
            }
        }

        internal override void Validate()
        {
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

            if (this.MaxQueuePollingInterval <= TimeSpan.Zero)
            {
                throw new InvalidOperationException($"{nameof(this.MaxQueuePollingInterval)} must be non-negative.");
            }
        }

        internal override void AddToDebugString(StringBuilder builder)
        {
            builder.Append(nameof(this.ConnectionStringName)).Append(": ").Append(this.ConnectionStringName).Append(", ");
            builder.Append(nameof(this.PartitionCount)).Append(": ").Append(this.PartitionCount).Append(", ");
            builder.Append(nameof(this.ControlQueueBatchSize)).Append(": ").Append(this.ControlQueueBatchSize).Append(", ");
            builder.Append(nameof(this.ControlQueueVisibilityTimeout)).Append(": ").Append(this.ControlQueueVisibilityTimeout).Append(", ");
            builder.Append(nameof(this.WorkItemQueueVisibilityTimeout)).Append(": ").Append(this.WorkItemQueueVisibilityTimeout).Append(", ");
            builder.Append(nameof(this.TrackingStoreConnectionStringName)).Append(": ").Append(this.TrackingStoreConnectionStringName).Append(", ");
            if (!string.IsNullOrEmpty(this.TrackingStoreConnectionStringName))
            {
                builder.Append(nameof(this.TrackingStoreNamePrefix)).Append(": ").Append(this.TrackingStoreNamePrefix).Append(", ");
            }

            builder.Append(nameof(this.MaxQueuePollingInterval)).Append(": ").Append(this.MaxQueuePollingInterval).Append(", ");
        }
    }
}
