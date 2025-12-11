// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    public class DurableTaskTriggersScaleProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private const string DefaultConnectionName = "connectionName";
        private const string ConnectionNameOverride = "connectionStringName";

        private readonly IScaleMonitor monitor;
        private readonly ITargetScaler targetScaler;

        public DurableTaskTriggersScaleProvider(
            INameResolver nameResolver,
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

            // Resolve app settings (e.g., %MyConnectionString% -> actual value)
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
            string? connectionName = GetConnectionNameFromOptions(metadata.StorageProvider) ?? scalabilityProviderFactory.DefaultConnectionName;

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

        private static string? GetConnectionNameFromOptions(IDictionary<string, object>? storageProvider)
        {
            if (storageProvider == null)
            {
                return null;
            }

            // Try connectionName first
            if (storageProvider.TryGetValue(DefaultConnectionName, out object? value1) && value1 is string s1 && !string.IsNullOrWhiteSpace(s1))
            {
                return s1;
            }

            // Try connectionStringName
            if (storageProvider.TryGetValue(ConnectionNameOverride, out object? value2) && value2 is string s2 && !string.IsNullOrWhiteSpace(s2))
            {
                return s2;
            }

            return null;
        }

        public IScaleMonitor GetMonitor()
        {
            return this.monitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this.targetScaler;
        }
    }
}