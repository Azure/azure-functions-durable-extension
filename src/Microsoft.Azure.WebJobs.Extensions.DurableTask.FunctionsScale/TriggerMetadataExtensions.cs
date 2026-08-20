// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale
{
    /// <summary>
    /// Extension methods for <see cref="TriggerMetadata"/>.
    /// </summary>
    internal static class TriggerMetadataExtensions
    {
        private const string DefaultConnectionName = "connectionName";
        private const string ConnectionNameOverride = "connectionStringName";

        /// <summary>
        /// For testing. Extracts DurableTaskMetadata from trigger metadata sent by the Scale Controller.
        /// </summary>
        /// <param name="triggerMetadata">The trigger metadata containing configuration from the Scale Controller.</param>
        /// <returns>The parsed metadata, or null if metadata is not available.</returns>
        public static DurableTaskMetadata? ExtractDurableTaskMetadata(this TriggerMetadata? triggerMetadata)
        {
            if (triggerMetadata?.Metadata == null)
            {
                return null;
            }

            return triggerMetadata.Metadata.ToObject<DurableTaskMetadata>();
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

            if (storageProvider.TryGetValue(DefaultConnectionName, out object? v1) && v1 is string s1 && !string.IsNullOrWhiteSpace(s1))
            {
                return s1;
            }

            if (storageProvider.TryGetValue(ConnectionNameOverride, out object? v2) && v2 is string s2 && !string.IsNullOrWhiteSpace(s2))
            {
                return s2;
            }

            return null;
        }

        /// <summary>
        /// Extracts a <see cref="global::Azure.Core.TokenCredential"/> from TriggerMetadata.
        /// </summary>
        /// <param name="triggerMetadata">The trigger metadata provided by the Scale Controller.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <returns>The extracted token credential, or <see langword="null"/> if unavailable.</returns>
        public static global::Azure.Core.TokenCredential? ExtractTokenCredential(TriggerMetadata? triggerMetadata, ILogger? logger)
        {
            if (triggerMetadata?.Properties == null)
            {
                return null;
            }

            // Check if metadata contains an AzureComponentFactory wrapper
            // ScaleController passes it as: metadata.Properties[nameof(AzureComponentFactory)] = new AzureComponentFactoryWrapper(...)
            if (triggerMetadata.Properties.TryGetValue("AzureComponentFactory", out object? componentFactoryObj) && componentFactoryObj != null)
            {
                // The AzureComponentFactoryWrapper has CreateTokenCredential method
                // Call it using reflection to get the TokenCredential
                var factoryType = componentFactoryObj.GetType();
                var method = factoryType.GetMethod("CreateTokenCredential");

                if (method == null)
                {
                    logger?.LogWarning(
                        "CreateTokenCredential method not found on the AzureComponentFactory instance provided by the scale controller. " +
                        "Identity-based authentication cannot be used in this case.");
                    return null;
                }

                try
                {
                    // Call CreateTokenCredential(null) to get the TokenCredential from the wrapper
                    var credential = method.Invoke(componentFactoryObj, new object?[] { null });
                    if (credential is global::Azure.Core.TokenCredential tokenCredential)
                    {
                        return tokenCredential;
                    }
                }
                catch (TargetInvocationException ex)
                {
                    logger?.LogWarning(ex, "Failed to extract TokenCredential from AzureComponentFactory. Using null credential instead.");
                    return null;
                }
                catch (TargetException ex)
                {
                    logger?.LogWarning(ex, "Failed to extract TokenCredential from AzureComponentFactory. Using null credential instead.");
                    return null;
                }
            }

            return null;
        }
    }
}