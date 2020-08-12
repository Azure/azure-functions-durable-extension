// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration options for Managed Identity.
    /// </summary>
    public class ManagedIdentityOptions
    {
       /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityOptions"/> class.
        /// </summary>
        /// <param name="authorityHost">The host of the Azure Active Directory authority.</param>
        /// <param name="tenantId">The tenant id of the user to authenticate.</param>
        public ManagedIdentityOptions(Uri authorityHost = null, string tenantId = null)
        {
            this.AuthorityHost = authorityHost;
            this.TenantId = tenantId;
        }

        /// <summary>
        /// The host of the Azure Active Directory authority. The default is https://login.microsoftonline.com/.
        /// </summary>
        [JsonProperty("authorityhost")]
        public Uri AuthorityHost { get; set; }

        /// <summary>
        /// The tenant id of the user to authenticate.
        /// </summary>
        [JsonProperty("tenantid")]
        public string TenantId { get; set; }
    }
}
