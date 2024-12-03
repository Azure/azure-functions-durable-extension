// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#nullable enable
using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Options
{
    /// <summary>
    /// Represents the base connection options for all Azure Storage Service clients.
    /// </summary>
    internal abstract class StorageServiceConnectionOptions
    {
        // This type is read from the user-specified connection configuration section for each of the Azure Storage
        // Service types: blob, queue, and table. Once read, the information is used to create the corresponding service client.

        /// <summary>
        /// Gets or sets the optional account name.
        /// </summary>
        /// <value>The Azure Storage account name if specified; otherwise <see langword="null"/>.</value>
        public string? AccountName { get; set; }

        /// <summary>
        /// Gets the optional service URI, if it can be derived from <see cref="AccountName"/>.
        /// </summary>
        /// <remarks>
        /// This value is overridden by the derived classes, as each client has its own name for the
        /// service URI, like <c>"BlobServiceUri"</c>.
        /// </remarks>
        /// <value>The service URI if specified; otherwise <see langword="null"/>.</value>
        public virtual Uri? ServiceUri => string.IsNullOrEmpty(this.AccountName)
            ? null
            : new Uri(
                string.Format(CultureInfo.InvariantCulture, "https://{0}.{1}.core.windows.net", this.AccountName, this.ServiceName),
                UriKind.Absolute);

        /// <summary>
        /// Gets the name of the Azure Storage service represented by the options.
        /// </summary>
        /// <value>The name of the service used within the service URI.</value>
        protected abstract string ServiceName { get; }
    }
}
