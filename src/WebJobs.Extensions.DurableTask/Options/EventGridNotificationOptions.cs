// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration of the Event Grid notification options
    /// for the Durable Task Extension.
    /// </summary>
    public class EventGridNotificationOptions
    {
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
        public string TopicEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the name of the app setting containing the key used for authenticating with the Azure Event Grid custom topic at <see cref="TopicEndpoint"/>.
        /// </summary>
        /// <value>
        /// The name of the app setting that stores the Azure Event Grid key.
        /// </value>
        public string KeySettingName { get; set; }

        /// <summary>
        /// Gets or sets the Event Grid publish request retry count.
        /// </summary>
        /// <value>The number of retry attempts.</value>
        public int PublishRetryCount { get; set; }

        /// <summary>
        /// Gets orsets the Event Grid publish request retry interval.
        /// </summary>
        /// <value>A <see cref="TimeSpan"/> representing the retry interval. The default value is 5 minutes.</value>
        public TimeSpan PublishRetryInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the Event Grid publish request http status.
        /// </summary>
        /// <value>A list of HTTP status codes, e.g. 400, 403.</value>
        public int[] PublishRetryHttpStatus { get; set; }

        /// <summary>
        /// Gets or sets the event types that will be published to Event Grid.
        /// </summary>
        /// <value>
        /// A list of strings. Possible values 'Started', 'Completed', 'Failed', 'Terminated'.
        /// </value>
        public string[] PublishEventTypes { get; set; }

        internal void Validate()
        {
            if (this.PublishRetryInterval <= TimeSpan.Zero ||
                this.PublishRetryInterval > TimeSpan.FromMinutes(60))
            {
                throw new InvalidOperationException($"{nameof(this.PublishRetryInterval)} must be non-negative and no more than 60 minutes.");
            }
        }

        internal void AddToDebugString(StringBuilder builder)
        {
            builder.Append(nameof(this.TopicEndpoint)).Append(": ").Append(this.TopicEndpoint).Append(", ");
            if (!string.IsNullOrEmpty(this.TopicEndpoint))
            {
                builder.Append(nameof(this.KeySettingName)).Append(": ").Append(this.KeySettingName).Append(", ");
                builder.Append(nameof(this.PublishRetryCount)).Append(": ").Append(this.PublishRetryCount).Append(", ");
                builder.Append(nameof(this.PublishRetryInterval)).Append(": ").Append(this.PublishRetryInterval).Append(", ");
                builder.Append(nameof(this.PublishRetryHttpStatus)).Append(": ").Append(string.Join(", ", this.PublishRetryHttpStatus ?? new int[0])).Append(", ");
            }
        }
    }
}
