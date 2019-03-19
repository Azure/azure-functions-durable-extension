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

        internal CommonStorageProviderOptions GetConfiguredProvider()
        {
            if (this.configuredProvider == null)
            {
                var storageProviderOptions = new CommonStorageProviderOptions[] { this.AzureStorage, this.Emulator };
                var activeProviders = storageProviderOptions.Where(provider => provider != null);
                if (activeProviders.Count() != 1)
                {
                    throw new InvalidOperationException("There must be exactly one storage provider configured.");
                }

                this.configuredProvider = activeProviders.First();
            }

            return this.configuredProvider;
        }

        internal void Validate()
        {
            this.GetConfiguredProvider().Validate();
        }

        internal void AddToDebugString(StringBuilder builder)
        {
            if (this.AzureStorage != null)
            {
                builder.Append(nameof(this.AzureStorage)).Append(": { ");
                this.AzureStorage.AddToDebugString(builder);
                builder.Append(" }, ");
            }
            else if (this.AzureStorage != null)
            {
                builder.Append(nameof(this.Emulator)).Append(": { ");
                this.Emulator.AddToDebugString(builder);
                builder.Append(" }, ");
            }
        }
    }
}
