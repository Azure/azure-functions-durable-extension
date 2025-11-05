// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale
{
    /// <summary>
    /// Extension methods for <see cref="TriggerMetadata"/>.
    /// </summary>
    internal static class TriggerMetadataExtensions
    {
        /// <summary>
        /// Extracts DurableTaskScaleOptions from trigger metadata sent by the Scale Controller.
        /// </summary>
        /// <param name="triggerMetadata">The trigger metadata containing configuration from the Scale Controller.</param>
        /// <returns>The parsed options, or null if metadata is not available.</returns>
        public static DurableTaskScaleOptions? ExtractDurableTaskScaleOptions(this TriggerMetadata? triggerMetadata)
        {
            if (triggerMetadata?.Metadata == null)
            {
                return null;
            }

            try
            {
                // Parse the JSON metadata to extract configuration values
                var metadata = triggerMetadata.Metadata.ToObject<DurableTaskMetadata>();
                if (metadata == null)
                {
                    return null;
                }

                return new DurableTaskScaleOptions
                {
                    HubName = metadata.TaskHubName,
                    MaxConcurrentActivityFunctions = metadata.MaxConcurrentActivityFunctions,
                    MaxConcurrentOrchestratorFunctions = metadata.MaxConcurrentOrchestratorFunctions,
                    StorageProvider = metadata.StorageProvider,
                };
            }
            catch
            {
                // If parsing fails, return null and fall back to constructor options
                return null;
            }
        }

        /// <summary>
        /// Attempts to extract a connection name from the storage provider dictionary.
        /// Checks both "connectionName" and "connectionStringName" keys for compatibility.
        /// </summary>
        /// <param name="storageProvider">The storage provider configuration dictionary.</param>
        /// <returns>The connection name if found; otherwise, <see langword="null"/>.</returns>
        public static string? ResolveConnectionName(IDictionary<string, object>? storageProvider)
        {
            if (storageProvider == null)
            {
                return null;
            }

            if (storageProvider.TryGetValue("connectionName", out object? v1) && v1 is string s1 && !string.IsNullOrWhiteSpace(s1))
            {
                return s1;
            }

            if (storageProvider.TryGetValue("connectionStringName", out object? v2) && v2 is string s2 && !string.IsNullOrWhiteSpace(s2))
            {
                return s2;
            }

            return null;
        }
    }
}