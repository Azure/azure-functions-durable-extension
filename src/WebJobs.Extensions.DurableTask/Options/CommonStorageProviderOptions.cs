// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// A common set of properties that all storage providers share, as well as validation methods that
    /// all storage provider options must implement.
    /// </summary>
    public abstract class CommonStorageProviderOptions
    {
        /// <summary>
        /// Gets or sets the name of the Azure Storage connection string used to manage the underlying Azure Storage resources.
        /// </summary>
        /// <remarks>
        /// If not specified, the default behavior is to use the standard `AzureWebJobsStorage` connection string for all storage usage.
        /// </remarks>
        /// <value>The name of a connection string that exists in the app's application settings.</value>
        public string ConnectionStringName { get; set; }

        /// <summary>
        /// Throws an exception if the provided hub name violates any naming conventions for the storage provider.
        /// </summary>
        internal abstract void ValidateHubName(string hubName);

        /// <summary>
        /// Throws an exception if any of the settings of the storage provider are invalid.
        /// </summary>
        internal abstract void Validate();

        internal abstract void AddToDebugString(StringBuilder builder);
    }
}
