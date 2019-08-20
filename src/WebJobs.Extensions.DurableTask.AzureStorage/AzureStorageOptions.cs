// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the Azure Storage storage provider.
    /// </summary>
    public class AzureStorageOptions : IStorageOptions
    {
        /// <summary>
        /// Gets or sets the name of the Azure Storage connection string used to manage the underlying Azure Storage resources.
        /// </summary>
        /// <remarks>
        /// If not specified, the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string ConnectionStringName { get; set; }

        /// <inheritdoc/>
        public string ConnectionDetails => this.ConnectionStringName;

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
        /// If not specified, the <see cref="AzureStorageOptions.ConnectionStringName"/> connection string
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
        /// Gets or sets whether the extension will automatically fetch large messages in orchestration status
        /// queries. If set to false, the extension will return large messages as a blob url.
        /// </summary>
        /// <value>A boolean indicating whether will automatically fetch large messages .</value>
        public bool FetchLargeMessagesAutomatically { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum queue polling interval.
        /// </summary>
        /// <value>Maximum interval for polling control and work-item queues.</value>
        public TimeSpan MaxQueuePollingInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <inheritdoc/>
        public string StorageTypeName => "AzureStorage";

        /// <inheritdoc/>
        public void ValidateHubName(string hubName)
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

        /// <inheritdoc/>
        public void Validate()
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

        /// <inheritdoc/>
        public List<KeyValuePair<string, string>> GetValues()
        {
            var values = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(nameof(this.ConnectionDetails), this.ConnectionDetails),
                new KeyValuePair<string, string>(nameof(this.PartitionCount), this.PartitionCount.ToString()),
                new KeyValuePair<string, string>(nameof(this.ControlQueueBatchSize), this.ControlQueueBatchSize.ToString()),
                new KeyValuePair<string, string>(nameof(this.ControlQueueVisibilityTimeout), this.ControlQueueVisibilityTimeout.ToString()),
                new KeyValuePair<string, string>(nameof(this.WorkItemQueueVisibilityTimeout), this.WorkItemQueueVisibilityTimeout.ToString()),
                new KeyValuePair<string, string>(nameof(this.FetchLargeMessagesAutomatically), this.FetchLargeMessagesAutomatically.ToString()),
                new KeyValuePair<string, string>(nameof(this.MaxQueuePollingInterval), this.MaxQueuePollingInterval.ToString()),
            };

            if (!string.IsNullOrEmpty(this.TrackingStoreConnectionStringName))
            {
                values.Add(new KeyValuePair<string, string>(nameof(this.TrackingStoreConnectionStringName), this.TrackingStoreConnectionStringName));
                values.Add(new KeyValuePair<string, string>(nameof(this.TrackingStoreNamePrefix), this.TrackingStoreNamePrefix));
            }

            return values;
        }

        /// <inheritdoc/>
        public IOrchestrationServiceFactory GetOrchestrationServiceFactory()
        {
            throw new NotImplementedException();
        }
    }
}
