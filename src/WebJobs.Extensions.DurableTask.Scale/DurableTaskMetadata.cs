// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Represents the Durable Task configuration sent by the Scale Controller in the SyncTriggers payload.
    /// This is deserialized from triggerMetadata.Metadata and passed to factories via triggerMetadata.Properties.
    /// </summary>
    public class DurableTaskMetadata
    {
        /// <summary>
        /// Gets or sets the name of the Durable Task Hub. This identifies the taskhub being monitored or scaled.
        /// </summary>
        [JsonPropertyName("taskHubName")]
        public string? TaskHubName { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of orchestrator functions that can run concurrently on this worker instance.
        /// Used by the scale controller to balance orchestration and activity execution load.
        /// </summary>
        [JsonPropertyName("maxConcurrentOrchestratorFunctions")]
        public int? MaxConcurrentOrchestratorFunctions { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activity functions that can run concurrently on this worker instance.
        /// Used by the scale controller to balance orchestration and activity execution load.
        /// </summary>
        [JsonPropertyName("maxConcurrentActivityFunctions")]
        public int? MaxConcurrentActivityFunctions { get; set; }

        /// <summary>
        /// Gets or sets the storage provider configuration dictionary, typically containing connection and provider-specific options.
        /// </summary>
        [JsonPropertyName("storageProvider")]
        public IDictionary<string, object>? StorageProvider { get; set; }

        /// <summary>
        /// Resolves app settings in <see cref="DurableTaskMetadata"/> using the provided <see cref="INameResolver"/>.
        /// This allows configuration values such as connection strings to be expanded from environment variables or host settings.
        /// </summary>
        /// <param name="metadata">The scale options instance containing configuration values to resolve.</param>
        /// <param name="nameResolver">The name resolver used to resolve app setting placeholders.</param>
        public static void ResolveAppSettingOptions(DurableTaskMetadata metadata, INameResolver nameResolver)
        {
            if (metadata.StorageProvider != null &&
                metadata.StorageProvider.TryGetValue("connectionName", out object? connectionNameObj) &&
                connectionNameObj is string connectionName)
            {
                metadata.StorageProvider["connectionName"] = nameResolver.Resolve(connectionName) ?? string.Empty;
            }
        }

        /// <summary>
        /// Creates a DurableTaskMetadata instance from DurableTaskOptions for runtime-driven scaling.
        /// Extracts only the configuration needed for scaling decisions.
        /// </summary>
        /// <param name="options">The Durable Task options from host.json.</param>
        /// <returns>A DurableTaskMetadata instance with configuration for scaling.</returns>
        public static DurableTaskMetadata FromOptions(object options)
        {
            // Use reflection to extract values since we can't reference WebJobs.Extensions.DurableTask from here
            var optionsType = options.GetType();
            var hubNameProp = optionsType.GetProperty("HubName");
            var maxConcurrentOrchestratorsProp = optionsType.GetProperty("MaxConcurrentOrchestratorFunctions");
            var maxConcurrentActivitiesProp = optionsType.GetProperty("MaxConcurrentActivityFunctions");
            var storageProviderProp = optionsType.GetProperty("StorageProvider");

            return new DurableTaskMetadata
            {
                TaskHubName = hubNameProp?.GetValue(options) as string,
                MaxConcurrentOrchestratorFunctions = maxConcurrentOrchestratorsProp?.GetValue(options) as int?,
                MaxConcurrentActivityFunctions = maxConcurrentActivitiesProp?.GetValue(options) as int?,
                StorageProvider = storageProviderProp?.GetValue(options) as IDictionary<string, object>,
            };
        }
    }
}
