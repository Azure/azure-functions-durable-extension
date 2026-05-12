// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// Provides scale monitoring and target scaling for Durable Task triggers
    /// by delegating to backend-specific scalability providers.
    /// </summary>
    public class DurableTaskTriggersScaleProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private readonly IScaleMonitor monitor;
        private readonly ITargetScaler targetScaler;

        /// <summary>
        /// Initializes a new instance of the <see cref="DurableTaskTriggersScaleProvider"/> class.
        /// </summary>
        /// <param name="nameResolver">
        /// Resolves application setting values referenced in trigger metadata.
        /// </param>
        /// <param name="loggerFactory">
        /// Factory used to create loggers for diagnostics.
        /// </param>
        /// <param name="scalabilityProviderFactories">
        /// A collection of scalability provider factories used to resolve
        /// the appropriate backend implementation.
        /// </param>
        /// <param name="triggerMetadata">
        /// The trigger metadata containing Durable Task configuration.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when required trigger metadata is missing or cannot be deserialized.
        /// </exception>
        public DurableTaskTriggersScaleProvider(
            Microsoft.Azure.WebJobs.INameResolver nameResolver,
            ILoggerFactory loggerFactory,
            IEnumerable<IScalabilityProviderFactory> scalabilityProviderFactories,
            TriggerMetadata triggerMetadata)
        {
            string functionId = triggerMetadata.FunctionName;
            var functionName = new FunctionName(functionId);

            // Deserialize the configuration from triggerMetadata
            var metadata = triggerMetadata.Metadata.ToObject<DurableTaskMetadata>()
                ?? throw new InvalidOperationException($"Failed to deserialize trigger metadata. Payload: {triggerMetadata.Metadata}");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(metadata.TaskHubName))
            {
                throw new InvalidOperationException($"Expected `taskHubName` property in SyncTriggers payload but found none.");
            }

            DurableTaskMetadata.ResolveAppSettingOptions(metadata, nameResolver);

            var logger = loggerFactory.CreateLogger<DurableTaskTriggersScaleProvider>();

            // Determine which scalability provider factory to use based on metadata.StorageProvider["type"]
            // If StorageProvider is null or doesn't contain "type", defaults to "AzureStorage" provider
            IScalabilityProviderFactory scalabilityProviderFactory = DurableTaskScaleExtension.GetScalabilityProviderFactory(
                metadata, logger, scalabilityProviderFactories);

            // Use the new overload that accepts pre-deserialized metadata to avoid double deserialization of the metadata payload
            // Still pass triggerMetadata to allow access to Properties (e.g., token credentials)
            ScalabilityProvider defaultscalabilityProvider = scalabilityProviderFactory.GetScalabilityProvider(metadata, triggerMetadata);

            // Get connection name from metadata.StorageProvider
            string? connectionName = TriggerMetadataExtensions.ResolveConnectionName(metadata.StorageProvider) ?? scalabilityProviderFactory.DefaultConnectionName;

            logger.LogInformation(
                "Creating DurableTaskTriggersScaleProvider for function {FunctionName}: connectionName = '{ConnectionName}'",
                triggerMetadata.FunctionName,
                connectionName);

            this.targetScaler = ScaleUtils.GetTargetScaler(
                defaultscalabilityProvider,
                functionId,
                functionName,
                connectionName,
                metadata.TaskHubName);

            this.monitor = ScaleUtils.GetScaleMonitor(
                defaultscalabilityProvider,
                functionId,
                functionName,
                connectionName,
                metadata.TaskHubName);
        }

        /// <summary>
        /// Gets the scale monitor used to observe load and trigger scaling decisions.
        /// </summary>
        /// <returns>
        /// An <see cref="IScaleMonitor"/> instance.
        /// </returns>
        public IScaleMonitor GetMonitor()
        {
            return this.monitor;
        }

        /// <summary>
        /// Gets the target scaler used to compute the desired worker count.
        /// </summary>
        /// <returns>
        /// An <see cref="ITargetScaler"/> instance.
        /// </returns>
        public ITargetScaler GetTargetScaler()
        {
            return this.targetScaler;
        }
    }
}
