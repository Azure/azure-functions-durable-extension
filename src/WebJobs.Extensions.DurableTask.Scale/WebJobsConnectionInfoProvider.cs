// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Resolves connection information from WebJobs configuration.
    /// </summary>
    internal class WebJobsConnectionInfoProvider : IConnectionInfoResolver
    {
        private readonly IConfiguration configuration;

        public WebJobsConnectionInfoProvider(IConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public (string ConnectionString, TokenCredential Credential) ResolveConnectionInfo(string connectionName)
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName));
            }

            // First, try to get the connection string directly
            var connectionString = this.configuration.GetConnectionString(connectionName) 
                                ?? this.configuration[connectionName];

            if (!string.IsNullOrEmpty(connectionString))
            {
                return (connectionString, null);
            }

            // If no connection string, check for service URI (Managed Identity scenario)
            var serviceUri = this.configuration[$"{connectionName}:serviceUri"] 
                          ?? this.configuration[$"{connectionName}__serviceUri"];

            if (!string.IsNullOrEmpty(serviceUri))
            {
                // Use DefaultAzureCredential for Managed Identity
                return (null, new DefaultAzureCredential());
            }

            // Return null if nothing found
            return (null, null);
        }
    }
}

