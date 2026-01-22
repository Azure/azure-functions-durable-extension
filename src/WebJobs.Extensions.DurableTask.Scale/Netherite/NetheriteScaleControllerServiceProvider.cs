// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Azure.Core;
using Microsoft.Extensions.Azure;
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
}
