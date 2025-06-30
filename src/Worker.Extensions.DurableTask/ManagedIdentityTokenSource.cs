// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

/// <summary>
/// Token Source implementation for Azure Managed Identities.
/// </summary>
public class ManagedIdentityTokenSource : TokenSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedIdentityTokenSource"/> class.
    /// </summary>
    /// <param name="resource">The Entra ID resource identifier of the web API being invoked.</param>
    /// <param name="options">Optional Azure credential options to use when authenticating.</param>
    public ManagedIdentityTokenSource(string resource, ManagedIdentityOptions? options = null)
        : base(NormalizeResource(resource))
    {
        this.Options = options;
    }

    /// <summary>
    /// The azure credential options that a user can configure when authenticating.
    /// </summary>
    [JsonPropertyName("options")]
    public ManagedIdentityOptions? Options { get; }

    // Normalizes the resource identifier for Azure Managed Identity token requests.
    // Ensures that well-known resources are suffixed with ".default" as required by Azure AD.
    private static string NormalizeResource(string resource)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        if (resource == "https://management.core.windows.net" || resource == "https://management.core.windows.net/")
        {
            return "https://management.core.windows.net/.default";
        }

        if (resource == "https://graph.microsoft.com" || resource == "https://graph.microsoft.com/")
        {
            return "https://graph.microsoft.com/.default";
        }

        return resource;
    }
}
