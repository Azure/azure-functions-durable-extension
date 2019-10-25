// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Configuration options for the various storage providers supported
    /// by the Durable Task Extension.
    /// </summary>
    public class StorageProviderOptions
    {
        private CommonStorageProviderOptions configuredProvider;

        /// <summary>
        /// The section for configuration related to the Azure Storage provider.
        /// </summary>
        public AzureStorageOptions AzureStorage { get; set; }

        /// <summary>
        /// The section for configuration related to the Emulator provider.
        /// </summary>
        public EmulatorStorageOptions Emulator { get; set; }

        /// <summary>
        /// The section for configuration related to the Redis provider.
        /// </summary>
        public RedisStorageOptions Redis { get; set; }

        internal CommonStorageProviderOptions GetConfiguredProvider()
        {
            if (this.configuredProvider == null)
            {
                var storageProviderOptions = new CommonStorageProviderOptions[] { this.AzureStorage, this.Emulator, this.Redis };
                var activeProviders = storageProviderOptions.Where(provider => provider != null);
                if (!activeProviders.Any())
                {
                    // Assume azure storage with defaults
                    this.AzureStorage = new AzureStorageOptions();
                    this.configuredProvider = this.AzureStorage;
                }
                else if (activeProviders.Count() > 1)
                {
                    throw new InvalidOperationException("Only one storage provider can be configured per function application.");
                }
                else
                {
                    this.configuredProvider = activeProviders.First();
                }
            }

            return this.configuredProvider;
        }

        internal void Validate()
        {
            this.GetConfiguredProvider().Validate();
        }

        internal void AddToDebugString(StringBuilder builder)
        {
            CommonStorageProviderOptions configuredProvider = this.GetConfiguredProvider();
            string providerName = configuredProvider.GetType().ToString();
            builder.Append(providerName).Append(": { ");
            this.configuredProvider.AddToDebugString(builder);
            builder.Append(" }, ");
        }
    }
}
