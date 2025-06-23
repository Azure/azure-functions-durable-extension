// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask
{
    /// <summary>
    /// Token Source implementation for Azure Managed Identities.
    /// This calss is the same as the one in WebJobs.Extensions.DurableTask/ManagedIdentityTokenSource.cs
    /// The implementation is kept in sync to ensure compatibility.
    /// </summary>
    public class ManagedIdentityTokenSource : ITokenSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ManagedIdentityTokenSource"/> class.
        /// </summary>
        /// <param name="resource">The Azure Active Directory resource identifier of the web API being invoked.</param>
        /// <param name="options">Optional Azure credential options to use when authenticating.</param>
        public ManagedIdentityTokenSource(string resource, ManagedIdentityOptions? options = null)
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
        /// </summary>
        [JsonPropertyName("resource")]
        public string Resource { get; }

        /// <summary>
        /// The azure credential options that a user can configure when authenticating.
        /// </summary>
        [JsonPropertyName("options")]
        public ManagedIdentityOptions? Options { get; }

        /// <summary>
        /// This method is not implemented as it will never be called.
        /// Token acquisition is handled by WebJobs.Extensions.DurableTask when it deserializes this and creates its own ManagedIdentityTokenSource instance.
        /// </summary>
        public Task<string> GetTokenAsync()
        {
            throw new NotImplementedException("GetTokenAsync is not implemented. Token acquisition is handled by WebJobs.Extensions.DurableTask.");
        }
    }
} 