// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Text;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the EventSourced storage provider.
    /// </summary>
    public class EventSourcedStorageOptions : CommonStorageProviderOptions
    {
        /// <summary>
        /// Gets or sets the name of the environment variable or configuration setting for the event-sourced backend.
        /// </summary>
        public string EventHubsConnectionStringName { get; set; }

        /// <summary>
        ///  Whether we are running in a test environment. In that case, we reset storage before the first test,
        ///  and we keep the event processor running between tests.
        /// </summary>
        public bool RunningInTestEnvironment { get; set; } = false;

        internal override void AddToDebugString(StringBuilder builder)
        {
            builder.Append(nameof(this.ConnectionStringName)).Append(": ").Append(this.ConnectionStringName);
        }

        internal override void Validate()
        {
            if (string.IsNullOrEmpty(this.ConnectionStringName))
            {
                throw new InvalidOperationException($"{nameof(EventSourcedStorageOptions.ConnectionStringName)} must be populated to use the EventSourced storage provider");
            }

            if (string.IsNullOrEmpty(this.EventHubsConnectionStringName))
            {
                throw new InvalidOperationException($"{nameof(EventSourcedStorageOptions.EventHubsConnectionStringName)} must be populated to use the EventSourced storage provider");
            }
        }

        internal override void ValidateHubName(string hubName)
        {
        }
    }
}
