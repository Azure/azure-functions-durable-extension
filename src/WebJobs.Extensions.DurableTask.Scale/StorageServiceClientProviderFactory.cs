// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Core;
using DurableTask.AzureStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Factory for creating azure storage client providers.
    /// </summary>
    internal class StorageServiceClientProviderFactory : IStorageServiceClientProviderFactory
    {
        private readonly IConfiguration configuration;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        public StorageServiceClientProviderFactory(
            IConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.logger = loggerFactory.CreateLogger<StorageServiceClientProviderFactory>();
        }

        public StorageAccountClientProvider GetClientProvider(string connectionName, TokenCredential tokenCredential = null)
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                throw new ArgumentNullException(nameof(connectionName));
            }

            // Priority 1: If tokenCredential is provided, check if Managed Identity is configured
            // (account name or service URIs). This takes precedence because if these are set,
            // it indicates an explicit intent to use Managed Identity.
            if (tokenCredential != null)
            {
                // 1.1: Try to get account name first (e.g., AzureWebJobsStorage__accountName)
                var accountName = this.configuration[$"{connectionName}__accountName"];
                if (!string.IsNullOrEmpty(accountName))
                {
                    this.logger.LogInformation("Using Managed Identity with account name for connection: {ConnectionName}, account: {AccountName}", connectionName, accountName);
                    return new StorageAccountClientProvider(accountName, tokenCredential);
                }

                // 1.2: Try to get service URIs (e.g., AzureWebJobsStorage__blobServiceUri, __queueServiceUri, __tableServiceUri)
                var blobServiceUri = this.configuration[$"{connectionName}__blobServiceUri"];
                var queueServiceUri = this.configuration[$"{connectionName}__queueServiceUri"];
                var tableServiceUri = this.configuration[$"{connectionName}__tableServiceUri"];

                if (!string.IsNullOrEmpty(blobServiceUri) && !string.IsNullOrEmpty(queueServiceUri) && !string.IsNullOrEmpty(tableServiceUri))
                {
                    this.logger.LogInformation("Using Managed Identity with service URIs for connection: {ConnectionName}", connectionName);
                    return new StorageAccountClientProvider(
                        new Uri(blobServiceUri),
                        new Uri(queueServiceUri),
                        new Uri(tableServiceUri),
                        tokenCredential);
                }

                // If tokenCredential is provided but no account name or service URIs are configured,
                // ignore the tokenCredential and fall through to use connection string instead.
                // This handles the case where Scale Controller provides DefaultAzureCredential
                // but we should use connection string.
                this.logger.LogInformation(
                    "TokenCredential provided but no account name or service URIs found for connection: {ConnectionName}. " +
                    "Falling back to connection string authentication.",
                    connectionName);
            }

            // Priority 2: Use connection string (default approach)
            var connectionString = this.configuration.GetConnectionString(connectionName) ?? this.configuration[connectionName];
            if (!string.IsNullOrEmpty(connectionString))
            {
                this.logger.LogInformation("Using connection string authentication for connection: {ConnectionName}", connectionName);
                return new StorageAccountClientProvider(connectionString);
            }

            // No valid authentication method found
            throw new InvalidOperationException(
                $"Could not find valid authentication configuration for connection: {connectionName}. " +
                $"Please provide either: " +
                $"(1) A connection string, or " +
                $"(2) TokenCredential with account name ('{connectionName}__accountName') or service URIs " +
                $"('{connectionName}__blobServiceUri', '{connectionName}__queueServiceUri', '{connectionName}__tableServiceUri').");
        }
    }
}
