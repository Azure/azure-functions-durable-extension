// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Data.Common;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Netherite
{
    /// <summary>
    /// Connection string to connect to Netherite backend service.
    /// </summary>
    public sealed class NetheriteConnectionString
    {
        private readonly DbConnectionStringBuilder builder;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetheriteConnectionString"/> class.
        /// </summary>
        /// <param name="connectionString">A connection string for a Netherite durable task service.</param>
        public NetheriteConnectionString(string connectionString)
        {
            this.builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        /// <summary>
        /// Gets the storage connection name specified in the connection string (if any).
        /// </summary>
        public string StorageConnectionName => this.GetValue("StorageConnectionName");

        /// <summary>
        /// Gets the Event Hubs connection name specified in the connection string (if any).
        /// </summary>
        public string EventHubsConnectionName => this.GetValue("EventHubsConnectionName");

        /// <summary>
        /// Gets the task hub name specified in the connection string (if any).
        /// </summary>
        public string TaskHubName => this.GetValue("TaskHub");

        /// <summary>
        /// Gets the hub name specified in the connection string (if any).
        /// </summary>
        public string HubName => this.GetValue("HubName");

        private string GetValue(string name)
        {
            // Case-insensitive lookup
            foreach (string key in this.builder.Keys)
            {
                if (string.Equals(key, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return this.builder.TryGetValue(key, out object value)
                        ? value as string
                        : null;
                }
            }

            return null;
        }
    }
}
