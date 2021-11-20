// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth
{
    /// <summary>
    /// Represents the identity of an Azure service, application, or user.
    /// </summary>
    public class AzureIdentityOptions
    {
        /// <summary>
        /// Gets or sets the identifier for the Azure Active Directory (AAD) tenant.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Gets or sets the identifier for the application (client).
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the application client's secret.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets a moniker indicating the type of authentication to be used.
        /// </summary>
        public string Credential { get; set; }

        /// <summary>
        /// Gets or sets the thumbprint of the certificate associated with the application (client).
        /// </summary>
        public string Certificate { get; set; }

        /// <summary>
        /// Gets or sets the location of the <see cref="Certificate"/>.
        /// </summary>
        public StoreLocation ClientCertificateStoreLocation { get; set; } = StoreLocation.CurrentUser;

        internal bool UseManagedIdentity => string.Equals(this.Credential, "managedidentity", StringComparison.OrdinalIgnoreCase);
    }
}
