// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Text;
using DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the EventHubs storage provider.
    /// </summary>
    public class EventHubsStorageOptions : CommonStorageProviderOptions
    {
        /// <summary>
        /// Gets or sets the name of the environment variable or configuration setting for the.
        /// </summary>
        public string EventHubsConnectionStringName { get; set; } = "AzureWebJobsEventHubs";

        /// <summary>
        /// Gets or sets the maximum number of work items that can be processed concurrently on a single node.
        /// The default value is 100.
        /// </summary>
        public int MaxConcurrentTaskActivityWorkItems { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum number of orchestrations that can be processed concurrently on a single node.
        /// The default value is 100.
        /// </summary>
        public int MaxConcurrentTaskOrchestrationWorkItems { get; set; } = 100;

        /// <summary>
        ///  Should we carry over unexecuted raised events to the next iteration of an orchestration on ContinueAsNew
        /// </summary>
        public BehaviorOnContinueAsNew EventBehaviourForContinueAsNew { get; set; } = BehaviorOnContinueAsNew.Carryover;

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
                throw new InvalidOperationException($"{nameof(EventHubsStorageOptions.ConnectionStringName)} must be populated to use the Redis storage provider");
            }
        }

        internal override void ValidateHubName(string hubName)
        {
        }
    }
}
