// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// Configuration options for ManagedIdentityTokenSource.
/// </summary>
public class ManagedIdentityOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedIdentityOptions"/> class.
    /// </summary>
    /// <param name="authorityHost">The host of the Entra ID authority.</param>
    /// <param name="tenantId">The tenant id of the user to authenticate.</param>
    public ManagedIdentityOptions(Uri? authorityHost = null, string? tenantId = null)
    {
        this.AuthorityHost = authorityHost;
        this.TenantId = tenantId;
    }

    /// <summary>
    /// The host of the Entra ID authority. The default is https://login.microsoftonline.com/.
    /// </summary>
    [JsonPropertyName("authorityhost")]
    public Uri? AuthorityHost { get; set; }

    /// <summary>
    /// The tenant id of the user to authenticate.
    /// </summary>
    [JsonPropertyName("tenantid")]
    public string? TenantId { get; set; }
}
