using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Represents the Durable Task configuration sent by the Scale Controller in the SyncTriggers payload.
    /// This is deserialized from triggerMetadata.Metadata and passed to factories via triggerMetadata.Properties.
    /// </summary>
    public class DurableTaskMetadata
    {
        /// <summary>
        /// Gets or sets the name of the Durable Task Hub used by the function app.
        /// </summary>
        [JsonPropertyName("taskHubName")]
        public string? TaskHubName { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent orchestrator.
        /// </summary>
        [JsonPropertyName("maxConcurrentOrchestratorFunctions")]
        public int? MaxConcurrentOrchestratorFunctions { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent activity.
        /// </summary>
        [JsonPropertyName("maxConcurrentActivityFunctions")]
        public int? MaxConcurrentActivityFunctions { get; set; }

        /// <summary>
        /// Gets or sets the storage provider configuration dictionary, typically containing connection and provider-specific options.
        /// </summary>
        [JsonPropertyName("storageProvider")]
        public IDictionary<string, object>? StorageProvider { get; set; }
    }
}
