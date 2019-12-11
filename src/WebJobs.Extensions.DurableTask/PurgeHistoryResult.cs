// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Class to hold statistics about this execution of purge history.
    /// </summary>
    [DataContract]
    public class PurgeHistoryResult
    {
        /// <summary>
        /// Constructor for purge history statistics.
        /// </summary>
        /// <param name="instancesDeleted">Number of instances deleted.</param>
        public PurgeHistoryResult(int instancesDeleted)
        {
            this.InstancesDeleted = instancesDeleted;
        }

        /// <summary>
        /// Gets the number of deleted instances.
        /// </summary>
        /// <value>The number of deleted instances.</value>
        [DataMember(Name = "instancesDeleted")]
        public int InstancesDeleted { get; }
    }
}
