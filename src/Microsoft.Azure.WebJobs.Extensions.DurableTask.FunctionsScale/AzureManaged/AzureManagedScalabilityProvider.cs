// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.DurableTask.AzureManagedBackend;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureManaged
{
    /// <summary>
    /// The AzureManaged backend implementation of the scalability provider for Durable Functions.
    /// </summary>
    public class AzureManagedScalabilityProvider : ScalabilityProvider
    {
        private readonly AzureManagedOrchestrationService orchestrationService;
        private readonly string connectionName;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureManagedScalabilityProvider"/> class.
        /// </summary>
        /// <param name="orchestrationService">
        /// The <see cref="AzureManagedOrchestrationService"/> instance that provides access to backend service for scaling operations.
        /// </param>
        /// <param name="connectionName">
        /// The logical name of the storage or service connection associated with this provider.
        /// </param>
        /// <param name="logger">
        /// The <see cref="ILogger"/> instance used for logging provider activities and diagnostics.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="orchestrationService"/> is <see langword="null"/>.
        /// </exception>
        public AzureManagedScalabilityProvider(
            AzureManagedOrchestrationService orchestrationService,
            string connectionName,
            ILogger logger)
            : base("AzureManaged", connectionName)
        {
            this.orchestrationService = orchestrationService ?? throw new ArgumentNullException(nameof(orchestrationService));
            this.connectionName = connectionName;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the app setting containing the Azure Managed connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        /// <inheritdoc/>
        /// This is not used.
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out IScaleMonitor scaleMonitor)
        {
            // Azure Managed backend does not support the legacy scale monitor infrastructure.
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
            // Create a target scaler that uses the orchestration service's metrics endpoint.
            // All target scalers share the same AzureManagedOrchestrationService in the same task hub.
            targetScaler = new AzureManagedTargetScaler(this.orchestrationService, functionId, this.logger);
            return true;
        }
    }
}
