// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.Netherite;
using DurableTask.Netherite.Scaling;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite
{
    /// <summary>
    /// The Netherite backend implementation of the scalability provider for Durable Functions.
    /// </summary>
    public class NetheriteScalabilityProvider : ScalabilityProvider
    {
        private readonly NetheriteOrchestrationServiceSettings settings;
        private readonly string connectionName;
        private readonly ILogger logger;

        private readonly object initLock = new object();
        private NetheriteMetricsProvider singletonNetheriteMetricsProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetheriteScalabilityProvider"/> class.
        /// </summary>
        /// <param name="settings">
        /// The <see cref="NetheriteOrchestrationServiceSettings"/> used to configure the service.
        /// </param>
        /// <param name="connectionName">
        /// The logical name of the storage or service connection associated with this provider.
        /// </param>
        /// <param name="logger">
        /// The <see cref="ILogger"/> instance used for logging provider activities and diagnostics.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="settings"/> is <see langword="null"/>.
        /// </exception>
        public NetheriteScalabilityProvider(
            NetheriteOrchestrationServiceSettings settings,
            string connectionName,
            ILogger logger)
            : base("Netherite", connectionName)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.connectionName = connectionName;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the app setting containing the Netherite connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        /// <inheritdoc/>
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out IScaleMonitor scaleMonitor)
        {
            // Netherite backend does not support the legacy scale monitor infrastructure.
            // Return false so that the scaling utilities can provide a no-op monitor.
            scaleMonitor = null;
            return false;
        }

        /// <inheritdoc/>
        public override bool TryGetTargetScaler(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out ITargetScaler targetScaler)
        {
            lock (this.initLock)
            {
                if (this.singletonNetheriteMetricsProvider == null)
                {
                    // Create the load publisher based on settings using the public classes from Netherite
                    ILoadPublisherService loadPublisher = string.IsNullOrEmpty(this.settings.LoadInformationAzureTableName) ?
                        (ILoadPublisherService)new AzureBlobLoadPublisher(
                            this.settings.BlobStorageConnection,
                            this.settings.HubName,
                            this.settings.TaskhubParametersFilePath)
                        : new AzureTableLoadPublisher(
                            this.settings.TableStorageConnection,
                            this.settings.LoadInformationAzureTableName,
                            this.settings.HubName);

                    // Create the Netherite metrics provider
                    this.singletonNetheriteMetricsProvider = new NetheriteMetricsProvider(
                        loadPublisher,
                        this.settings.EventHubsConnection);
                }

                // Create a target scaler that uses the orchestration service's metrics endpoint.
                // All target scalers share the same NetheriteMetricsProvider in the same task hub.
                targetScaler = new NetheriteTargetScaler(functionId, this.singletonNetheriteMetricsProvider, this, this.logger);
                return true;
            }
        }
    }
}
