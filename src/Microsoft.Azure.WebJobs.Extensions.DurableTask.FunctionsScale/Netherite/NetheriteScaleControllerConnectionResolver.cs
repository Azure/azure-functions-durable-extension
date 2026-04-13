// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Core;
using DurableTask.Netherite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite
{
    /// <summary>
    /// A <see cref="ConnectionResolver"/> for Netherite Scale Controller scenarios.
    /// Supports both connection-string-based and identity-based (token credential) authentication
    /// by inspecting configuration sections following the Azure Functions WebJobs conventions.
    /// </summary>
    /// <remarks>
    /// This mirrors the behavior of <c>ConfigurationSectionBasedConnectionNameResolver</c> in the
    /// Netherite repo, adapted for the Scale Controller context.
    /// </remarks>
    internal class NetheriteScaleControllerConnectionResolver : ConnectionResolver
    {
        private const string WebJobsPrefix = "AzureWebJobs";

        private readonly IConfiguration configuration;
        private readonly TokenCredential? storageTokenCredential;
        private readonly TokenCredential? eventHubsTokenCredential;
        private readonly ILogger logger;

        public NetheriteScaleControllerConnectionResolver(
            IConfiguration configuration,
            TokenCredential? storageTokenCredential,
            TokenCredential? eventHubsTokenCredential,
            ILogger logger)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.storageTokenCredential = storageTokenCredential;
            this.eventHubsTokenCredential = eventHubsTokenCredential;
        }

        /// <summary>
        /// Called by <c>Validate</c> to resolve a named connection into a <see cref="ConnectionInfo"/>.
        /// Dispatches to <c>ResolveStorageConnection</c> or <c>ResolveEventHubsConnection</c>
        /// based on the resource type.
        /// </summary>
        public override ConnectionInfo? ResolveConnectionInfo(string taskHub, string connectionName, ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.BlobStorage:
                case ResourceType.TableStorage:
                    return this.ResolveStorageConnection(connectionName, resourceType);

                case ResourceType.PageBlobStorage:
                    // PageBlobStorage is used by FASTER's internal log, not by the scaling path.
                    // The scaling provider only reads load info from blobs/tables and partition
                    // info from Event Hubs, so this connection is not needed and we return null.
                    return null!;

                case ResourceType.EventHubsNamespace:
                    return this.ResolveEventHubsConnection(connectionName);

                default:
                    throw new NotSupportedException($"Unknown resource type: {resourceType}");
            }
        }

        /// <summary>
        /// Called by Validate first (before any <see cref="ResolveConnectionInfo"/> calls)
        /// with settings.EventHubsConnectionName to determine the storage and transport
        /// layer choices. Always returns Faster + EventHubs because those are the only
        /// layer choices relevant to the functions scale controller path.
        /// </summary>
        public override void ResolveLayerConfiguration(string connectionName, out StorageChoices storageChoice, out TransportChoices transportChoice)
        {
            storageChoice = StorageChoices.Faster;
            transportChoice = TransportChoices.EventHubs;
        }

        /// <summary>
        /// Resolves an Azure Storage connection (blob or table) by looking up the configuration
        /// section for <paramref name="connectionName"/>.
        /// If the section has a plain string value, it is treated as a connection string.
        /// If the section has sub-keys (accountName, blobServiceUri, tableServiceUri),
        /// the storage token credential is paired with the endpoint.
        /// </summary>
        private ConnectionInfo? ResolveStorageConnection(string connectionName, ResourceType resourceType)
        {
            IConfigurationSection connectionSection = this.GetWebJobsConnectionSection(connectionName);

            if (!string.IsNullOrWhiteSpace(connectionSection?.Value))
            {
                this.logger.LogDebug("Resolved storage connection '{Connection}' via connection string.", connectionName);
                return ConnectionInfo.FromStorageConnectionString(connectionSection.Value, resourceType);
            }

            if (this.storageTokenCredential != null && connectionSection != null && connectionSection.Exists())
            {
                string? accountName = connectionSection["accountName"];
                if (!string.IsNullOrEmpty(accountName))
                {
                    this.logger.LogDebug(
                        "Resolved storage connection '{Connection}' via token credential with account name '{Account}'.",
                        connectionName,
                        accountName);
                    return ConnectionInfo.FromTokenCredential(this.storageTokenCredential, accountName, resourceType);
                }

                string? host = null;
                if (resourceType == ResourceType.TableStorage)
                {
                    string? tableServiceUri = connectionSection["tableServiceUri"];
                    if (Uri.TryCreate(tableServiceUri, UriKind.Absolute, out var uri))
                    {
                        host = uri.Host;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Invalid tableServiceUri configuration: '{tableServiceUri}'");
                    }
                }

                if (string.IsNullOrEmpty(host))
                {
                    string? blobServiceUri = connectionSection["blobServiceUri"];
                    if (!string.IsNullOrEmpty(blobServiceUri))
                    {
                        if (Uri.TryCreate(blobServiceUri, UriKind.Absolute, out var uri))
                        {
                            host = uri.Host;
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"Invalid blobServiceUri configuration: '{blobServiceUri}'");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(host))
                {
                    this.logger.LogDebug(
                        "Resolved storage connection '{Connection}' via token credential with host '{Host}'.",
                        connectionName,
                        host);
                    return ConnectionInfo.FromTokenCredentialAndHost(this.storageTokenCredential, host, resourceType);
                }
            }

            this.logger.LogWarning(
                "Unable to resolve storage connection '{Connection}'. This may indicate a misconfiguration. " +
                "Ensure the connection is defined as a connection string, or as a section with 'accountName', 'blobServiceUri', or 'tableServiceUri' sub-keys.",
                connectionName);
            return null;
        }

        /// <summary>
        /// Resolves an Event Hubs namespace connection by looking up the configuration
        /// section for <paramref name="connectionName"/>.
        /// If the section has a plain string value, it is treated as a connection string.
        /// If the section has a fullyQualifiedNamespace sub-key, the Event Hubs token
        /// credential (or storage credential as fallback) is paired with the namespace.
        /// </summary>
        private ConnectionInfo? ResolveEventHubsConnection(string connectionName)
        {
            IConfigurationSection connectionSection = this.GetWebJobsConnectionSection(connectionName);

            if (!string.IsNullOrWhiteSpace(connectionSection?.Value))
            {
                this.logger.LogDebug("Resolved Event Hubs connection '{Connection}' via connection string.", connectionName);
                return ConnectionInfo.FromEventHubsConnectionString(connectionSection.Value);
            }

            TokenCredential? credential = this.eventHubsTokenCredential ?? this.storageTokenCredential;
            if (credential != null && connectionSection != null && connectionSection.Exists())
            {
                string? fullyQualifiedNamespace = connectionSection["fullyQualifiedNamespace"];
                if (!string.IsNullOrEmpty(fullyQualifiedNamespace))
                {
                    this.logger.LogDebug(
                        "Resolved Event Hubs connection '{Connection}' via token credential with namespace '{Namespace}'.",
                        connectionName,
                        fullyQualifiedNamespace);
                    return ConnectionInfo.FromTokenCredentialAndHost(credential, fullyQualifiedNamespace, ResourceType.EventHubsNamespace);
                }
            }

            this.logger.LogWarning(
                "Unable to resolve Event Hubs connection '{Connection}'. This may indicate a misconfiguration. " +
                "Ensure the connection is defined as a connection string, or as a section with a 'fullyQualifiedNamespace' sub-key.",
                connectionName);
            return null;
        }

        /// <summary>
        /// Resolves a connection section following Azure Functions WebJobs conventions:
        /// first tries with <c>AzureWebJobs</c> prefix, then without.
        /// </summary>
        private IConfigurationSection GetWebJobsConnectionSection(string connectionName)
        {
            string prefixed = WebJobsPrefix + connectionName;
            IConfigurationSection section = this.GetConnectionStringOrSection(prefixed);
            if (section != null && section.Exists())
            {
                return section;
            }

            return this.GetConnectionStringOrSection(connectionName);
        }

        private IConfigurationSection GetConnectionStringOrSection(string name)
        {
            IConfigurationSection connectionStringSection = this.configuration.GetSection("ConnectionStrings").GetSection(name);
            if (connectionStringSection.Exists())
            {
                return connectionStringSection;
            }

            return this.configuration.GetSection(name);
        }
    }
}
