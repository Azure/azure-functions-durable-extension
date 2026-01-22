// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Core;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Netherite
{
    /// <summary>
    /// A minimal <see cref="IServiceProvider"/> implementation for Scale Controller scenarios.
    /// Provides an <see cref="AzureComponentFactory"/> that can return different credentials
    /// for Storage vs Event Hubs connections used by Netherite.
    /// </summary>
    /// <remarks>
    /// Netherite's <c>NetheriteOrchestrationService</c> constructor takes an <see cref="IServiceProvider"/>
    /// and uses it to resolve <see cref="AzureComponentFactory"/> for identity-based authentication.
    /// In Scale Controller scenarios (where no host <see cref="IServiceProvider"/> is available),
    /// this wrapper provides the credentials from <c>TriggerMetadata.Properties</c>.
    /// </remarks>
    internal class NetheriteScaleControllerServiceProvider : IServiceProvider
    {
        private readonly NetheriteScaleControllerAzureComponentFactory? componentFactoryWrapper;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetheriteScaleControllerServiceProvider"/> class.
        /// </summary>
        /// <param name="storageComponentFactory">The <see cref="AzureComponentFactory"/> for Storage credentials.</param>
        /// <param name="eventHubsCredentialFunc">Function to get Event Hubs credentials by connection name.</param>
        /// <param name="eventHubsConnectionName">The Event Hubs connection name to match.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public NetheriteScaleControllerServiceProvider(
            AzureComponentFactory? storageComponentFactory,
            Func<string, TokenCredential>? eventHubsCredentialFunc,
            string eventHubsConnectionName,
            ILogger logger)
        {
            if (storageComponentFactory != null || eventHubsCredentialFunc != null)
            {
                this.componentFactoryWrapper = new NetheriteScaleControllerAzureComponentFactory(
                    storageComponentFactory,
                    eventHubsCredentialFunc,
                    eventHubsConnectionName,
                    logger);
            }
        }

        /// <inheritdoc/>
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(AzureComponentFactory) && this.componentFactoryWrapper != null)
            {
                return this.componentFactoryWrapper;
            }

            return null;
        }
    }

    /// <summary>
    /// An <see cref="AzureComponentFactory"/> implementation that returns different credentials
    /// for Storage vs Event Hubs connections in Netherite Scale Controller scenarios.
    /// </summary>
    internal class NetheriteScaleControllerAzureComponentFactory : AzureComponentFactory
    {
        private readonly AzureComponentFactory? storageComponentFactory;
        private readonly Func<string, TokenCredential>? eventHubsCredentialFunc;
        private readonly string eventHubsConnectionName;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetheriteScaleControllerAzureComponentFactory"/> class.
        /// </summary>
        /// <param name="storageComponentFactory">The <see cref="AzureComponentFactory"/> for Storage credentials.</param>
        /// <param name="eventHubsCredentialFunc">Function to get Event Hubs credentials by connection name.</param>
        /// <param name="eventHubsConnectionName">The Event Hubs connection name to match.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public NetheriteScaleControllerAzureComponentFactory(
            AzureComponentFactory? storageComponentFactory,
            Func<string, TokenCredential>? eventHubsCredentialFunc,
            string eventHubsConnectionName,
            ILogger logger)
        {
            this.storageComponentFactory = storageComponentFactory;
            this.eventHubsCredentialFunc = eventHubsCredentialFunc;
            this.eventHubsConnectionName = eventHubsConnectionName;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override TokenCredential CreateTokenCredential(IConfiguration configuration)
        {
            // Determine if this is an Event Hubs connection request
            // Netherite passes configuration sections that contain the connection name
            string? connectionName = configuration?.GetSection("ConnectionName")?.Value;

            // If the configuration path or key contains the Event Hubs connection name, use Event Hubs credential
            if (!string.IsNullOrEmpty(connectionName) &&
                connectionName.Equals(this.eventHubsConnectionName, StringComparison.OrdinalIgnoreCase) &&
                this.eventHubsCredentialFunc != null)
            {
                try
                {
                    var credential = this.eventHubsCredentialFunc(connectionName);
                    if (credential != null)
                    {
                        this.logger.LogDebug("Using Event Hubs credential for connection '{Connection}'", connectionName);
                        return credential;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to get Event Hubs credential for connection '{Connection}'", connectionName);
                }
            }

            // Default to Storage credential from the wrapped factory
            if (this.storageComponentFactory != null)
            {
                this.logger.LogDebug("Using Storage credential from AzureComponentFactory");
                return this.storageComponentFactory.CreateTokenCredential(configuration);
            }

            throw new InvalidOperationException(
                "No credential available for Netherite identity-based authentication. " +
                "Ensure both Storage and Event Hubs credentials are configured.");
        }

        /// <inheritdoc/>
        public override object CreateClientOptions(Type optionsType, object serviceVersion, IConfiguration configuration)
        {
            if (this.storageComponentFactory != null)
            {
                return this.storageComponentFactory.CreateClientOptions(optionsType, serviceVersion, configuration);
            }

            throw new InvalidOperationException("No AzureComponentFactory available to create client options.");
        }

        /// <inheritdoc/>
        public override object CreateClient(Type clientType, IConfiguration configuration, TokenCredential credential, object clientOptions)
        {
            if (this.storageComponentFactory != null)
            {
                return this.storageComponentFactory.CreateClient(clientType, configuration, credential, clientOptions);
            }

            throw new InvalidOperationException("No AzureComponentFactory available to create client.");
        }
    }
}
