// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the Redis storage provider.
    /// </summary>
    public class RedisStorageOptions : IStorageOptions
    {
        /// <summary>
        /// Gets or sets the name of the Redis connection string used to manage the underlying Redis resources.
        /// </summary>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string ConnectionStringName { get; set; }

        /// <inheritdoc />
        public string ConnectionDetails => ConnectionStringName;

        /// <inheritdoc />
        public string StorageTypeName => "Redis";

        /// <inheritdoc />
        public IOrchestrationServiceFactory GetOrchestrationServiceFactory()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public List<KeyValuePair<string, string>> GetValues()
        {
            return new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>(nameof(ConnectionStringName), this.ConnectionStringName)
            };
        }

        /// <inheritdoc />
        public void Validate()
        {
            if (string.IsNullOrEmpty(this.ConnectionStringName))
            {
                throw new InvalidOperationException($"{nameof(RedisStorageOptions.ConnectionStringName)} must be populated to use the Redis storage provider");
            }
        }

        /// <inheritdoc />
        public void ValidateHubName(string hubName)
        {
            // NO OP
        }
    }
}
