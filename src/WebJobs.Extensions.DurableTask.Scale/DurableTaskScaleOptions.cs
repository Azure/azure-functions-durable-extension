// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Minimal options class for Scale package - only contains what's needed for scaling decisions.
    /// </summary>
    public class DurableTaskScaleOptions
    {
        /// <summary>
        /// Gets or sets the name of the Durable Task Hub.
        /// This identifies the taskhub being monitored or scaled.
        /// </summary>
        public string HubName { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of configuration settings for the underlying storage provider (e.g., Azure Storage, MSSQL, or Netherite).
        /// These settings typically include connection details and provider-specific parameters.
        /// </summary>
        public IDictionary<string, object> StorageProvider { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the maximum number of orchestrator functions that can run concurrently on this worker instance.
        /// Used by the scale controller to balance orchestration and activity execution load.
        /// </summary>
        public int? MaxConcurrentOrchestratorFunctions { get; set; }

        /// <summary>
        /// /// Gets or sets the maximum number of activity functions that can run concurrently on this worker instance.
        /// Used by the scale controller to balance orchestration and activity execution load.
        /// </summary>
        public int? MaxConcurrentActivityFunctions { get; set; }

        /// <summary>
        /// Resolves app settings in <see cref="DurableTaskScaleOptions"/> using the provided <see cref="INameResolver"/>.
        /// This allows configuration values such as connection strings to be expanded from environment variables or host settings.
        /// </summary>
        /// <param name="options">The scale options instance containing configuration values to resolve.</param>
        /// <param name="nameResolver">The name resolver used to resolve app setting placeholders.</param>
        public static void ResolveAppSettingOptions(DurableTaskScaleOptions options, INameResolver nameResolver)
        {
            if (options.StorageProvider.TryGetValue("connectionName", out object connectionNameObj) && connectionNameObj is string connectionName)
            {
                options.StorageProvider["connectionName"] = nameResolver.Resolve(connectionName);
            }
        }
    }
}
