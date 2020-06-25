// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Token Source implementation for Azure Managed Identities.
    /// </summary>
    public class ManagedIdentityTokenSource : ITokenSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenSource"/> class.
        /// </summary>
        /// <param name="resource">
        /// The Azure Active Directory resource identifier of the web API being invoked.
        /// For example, <c>https://management.core.windows.net/</c> or <c>https://graph.microsoft.com/</c>.
        /// </param>
        public ManagedIdentityTokenSource(string resource)
        {
            this.Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        }

        /// <summary>
        /// Gets the Azure Active Directory resource identifier of the web API being invoked.
        /// For example, <c>https://management.core.windows.net/</c> or <c>https://graph.microsoft.com/</c>.
        /// </summary>
        [JsonProperty("resource")]
        public string Resource { get; }

        /// <inheritdoc/>
        public async Task<string> GetTokenAsync()
        {
            var scopes = new string[] { this.Resource };
            TokenRequestContext context = new TokenRequestContext(scopes);

            ManagedIdentityCredential credential = new ManagedIdentityCredential();
            AccessToken token = await credential.GetTokenAsync(context);
            string accessToken = token.Token;

            return accessToken;
        }
    }
}