// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// Represents the Durable Task configuration sent by the Scale Controller in the SyncTriggers payload.
    /// This is deserialized from triggerMetadata.Metadata and passed to factories via triggerMetadata.Properties.
    /// </summary>
    public class DurableTaskMetadata
    {
        private const string DefaultConnectionName = "connectionName";
        private const string ConnectionNameOverride = "connectionStringName";

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
        /// Gets or sets the maximum number of entity functions that can run concurrently on this worker instance.
        /// Used by the scale controller to balance orchestration and entity execution load.
        /// </summary>
        [JsonPropertyName("maxConcurrentEntityFunctions")]
        public int? MaxConcurrentEntityFunctions { get; set; }

        /// <summary>
        /// Gets or sets the storage provider configuration dictionary, typically containing connection and provider-specific options.
        /// </summary>
        [JsonPropertyName("storageProvider")]
        public IDictionary<string, object>? StorageProvider { get; set; }
    }
}
