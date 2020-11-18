// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration options for the Azure Storage storage provider.
    /// </summary>
    public class AzureStorageOptions
    {
        // 45 alphanumeric characters gives us a buffer in our table/queue/blob container names.
        private const int MaxTaskHubNameSize = 45;
        private const int MinTaskHubNameSize = 3;
        private const string TaskHubPadding = "Hub";

        /// <summary>
        /// Gets or sets the name of the Azure Storage connection string used to manage the underlying Azure Storage resources.
        /// </summary>
        /// <remarks>
        /// If not specified, the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string ConnectionStringName { get; set; }

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
        /// Gets or set the number of control queue messages that can be buffered in memory
        /// at a time, at which point the dispatcher will wait before dequeuing any additional
        /// messages. The default is 256. The maximum value is 1000.
        /// </summary>
        /// <remarks>
        /// Increasing this value can improve orchestration throughput by pre-fetching more
        /// orchestration messages from control queues. The downside is that it increases the
        /// possibility of duplicate function executions if partition leases move between app
        /// instances. This most often occurs when the number of app instances changes.
        /// </remarks>
        /// <value>A non-negative integer between 0 and 1000. The default value is <c>256</c>.</value>
        public int ControlQueueBufferThreshold { get; set; } = 256;

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

        /// <summary>
        /// Determines whether or not to use the old partition management strategy, or the new
        /// strategy that is more resilient to split brain problems, at the potential expense
        /// of scale out performance.
        /// </summary>
        /// <value>A boolean indicating whether we use the legacy partition strategy. Defaults to false.</value>
        public bool UseLegacyPartitionManagement { get; set; } = false;

        /// <summary>
        /// Throws an exception if the provided hub name violates any naming conventions for the storage provider.
        /// </summary>
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
                throw new ArgumentException(GetTaskHubErrorString(hubName), e);
            }

            if (hubName.Length > 50)
            {
                throw new ArgumentException(GetTaskHubErrorString(hubName));
            }
        }

        private static string GetTaskHubErrorString(string hubName)
        {
            return $"Task hub name '{hubName}' should contain only alphanumeric characters, start with a letter, and have length between {MinTaskHubNameSize} and {MaxTaskHubNameSize}.";
        }

        internal bool IsSanitizedHubName(string hubName, out string sanitizedHubName)
        {
            // Only alphanumeric characters are valid.
            var validHubNameCharacters = hubName.ToCharArray().Where(char.IsLetterOrDigit);

            if (!validHubNameCharacters.Any())
            {
                sanitizedHubName = "DefaultTaskHub";
                return false;
            }

            // Azure Table storage requires that the task hub does not start with
            // a number. If it does, prepend "t" to the beginning.
            if (char.IsNumber(validHubNameCharacters.First()))
            {
                validHubNameCharacters = validHubNameCharacters.ToList();
                ((List<char>)validHubNameCharacters).Insert(0, 't');
            }

            sanitizedHubName = new string(validHubNameCharacters
                                .Take(MaxTaskHubNameSize)
                                .ToArray());

            if (sanitizedHubName.Length < MinTaskHubNameSize)
            {
                sanitizedHubName = sanitizedHubName + TaskHubPadding;
            }

            if (string.Equals(hubName, sanitizedHubName))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Throws an exception if any of the settings of the storage provider are invalid.
        /// </summary>
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

            if (this.ControlQueueBufferThreshold < 1 || this.ControlQueueBufferThreshold > 1000)
            {
                throw new InvalidOperationException($"{nameof(this.ControlQueueBufferThreshold)} must be between 1 and 1000.");
            }
        }
    }
}
