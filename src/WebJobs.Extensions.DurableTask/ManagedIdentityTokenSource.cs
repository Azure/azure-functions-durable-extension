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
        /// For example, <c>https://management.core.windows.net/.default</c> or <c>https://graph.microsoft.com/.default</c>.
        /// </param>
        /// <param name="options">Optional Azure credential options to use when authenticating.</param>
        public ManagedIdentityTokenSource(string resource, ManagedIdentityOptions options = null)
        {
            this.Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            this.Options = options;

            if (this.Resource.Equals("https://management.core.windows.net") || this.Resource.Equals("https://management.core.windows.net/"))
            {
                this.Resource = "https://management.core.windows.net/.default";
            }
            else if (this.Resource.Equals("https://graph.microsoft.com") || this.Resource.Equals("https://graph.microsoft.com/"))
            {
                this.Resource = "https://graph.microsoft.com/.default";
            }
        }

        /// <summary>
        /// Gets the Azure Active Directory resource identifier of the web API being invoked.
        /// For example, <c>https://management.core.windows.net/.default</c> or <c>https://graph.microsoft.com/.default</c>.
        /// </summary>
        [JsonProperty("resource")]
        public string Resource { get; }

        /// <summary>
        /// The azure credential options that a user can configure when authenticating.
        /// </summary>
        [JsonProperty("options")]
        public ManagedIdentityOptions Options { get; }

        /// <inheritdoc/>
        public async Task<string> GetTokenAsync()
        {
            var scopes = new string[] { this.Resource };
            TokenRequestContext context = new TokenRequestContext(scopes);

            DefaultAzureCredential defaultCredential;
            DefaultAzureCredentialOptions defaultAzureCredentialOptions = new DefaultAzureCredentialOptions();

            if (this.Options?.AuthorityHost != null)
            {
                defaultAzureCredentialOptions.AuthorityHost = this.Options.AuthorityHost;
            }

            if (!string.IsNullOrEmpty(this.Options?.TenantId))
            {
                defaultAzureCredentialOptions.InteractiveBrowserTenantId = this.Options.TenantId;
            }

            if (!string.IsNullOrEmpty(this.Options?.ClientId))
            {
                defaultAzureCredentialOptions.ManagedIdentityClientId = this.Options.ClientId;
            }

            defaultCredential = this.Options == null ? new DefaultAzureCredential() : new DefaultAzureCredential(defaultAzureCredentialOptions); // CodeQL [SM05137] Use DefaultAzureCredential explicitly for local development and is decided by the user

            AccessToken defaultToken = await defaultCredential.GetTokenAsync(context);
            string accessToken = defaultToken.Token;

            return accessToken;
        }
    }
}