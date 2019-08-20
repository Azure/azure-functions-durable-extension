// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Durable Task options for the Azure Storage flavored Durable Task extension.
    /// </summary>
    public class DurableTaskAzureStorageOptions : DurableTaskOptions
    {
        /// <summary>
        /// Section of configuration for the Azure Storage provider.
        /// </summary>
        public AzureStorageOptions AzureStorageProvider { get; set; } = new AzureStorageOptions();

        /// <inheritdoc/>
        public override IStorageOptions StorageOptions => this.AzureStorageProvider;
    }
}
