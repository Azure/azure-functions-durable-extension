// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Data.Common;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged
{
    /// <summary>
    /// Connection string to conenct to AzureManaged backend service.
    /// </summary>
    public sealed class AzureManagedConnectionString
    {
        private readonly DbConnectionStringBuilder builder;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureManagedConnectionString"/> class.
        /// </summary>
        /// <param name="connectionString">A connection string for an Azure-managed durable task service.</param>
        public AzureManagedConnectionString(string connectionString)
        {
            this.builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        /// <summary>
        /// Gets the authentication method specified in the connection string (if any).
        /// </summary>
        public string Authentication => this.GetValue("Authentication");

        /// <summary>
        /// Gets the managed identity or workload identity client ID specified in the connection string (if any).
        /// </summary>
        public string ClientId => this.GetValue("ClientID");

        /// <summary>
        /// Gets the endpoint specified in the connection string (if any).
        /// </summary>
        public string Endpoint => this.GetValue("Endpoint");

        /// <summary>
        /// Gets the task hub name specified in the connection string (if any).
        /// </summary>
        public string TaskHubName => this.GetValue("TaskHub");

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
