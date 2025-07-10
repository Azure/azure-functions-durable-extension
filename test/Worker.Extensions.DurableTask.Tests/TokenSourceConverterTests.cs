// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;

namespace Worker.Extensions.DurableTask.Tests;

// Tests for TokenSourceConverter
public class TokenSourceConverterTests
{
    [Fact]
    // Test that a ManagedIdentityTokenSource instance can be serialized correctly. 
    public void TokenSourceConverter_SerializeManagedIdentityTokenSource_ProducesCorrectJson()
    {
        var tokenSource = new ManagedIdentityTokenSource("https://management.core.windows.net/.default");
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TokenSourceConverter());

        string json = JsonSerializer.Serialize(tokenSource, options);

        Assert.Contains("\"kind\":\"AzureManagedIdentity\"", json);
        Assert.Contains("\"resource\":\"https://management.core.windows.net/.default\"", json);
    }

    [Fact]
    // Test that a ManagedIdentityTokenSource instance with options can be serialized correctly. 
    public void TokenSourceConverter_SerializeManagedIdentityTokenSourceWithOptions_ProducesCorrectJson()
    {
        var managedIdentityOptions = new ManagedIdentityOptions
        {
            AuthorityHost = new Uri("https://login.microsoftonline.com"),
            TenantId = "test-tenant-id"
        };
        var tokenSource = new ManagedIdentityTokenSource("https://graph.microsoft.com/.default", managedIdentityOptions);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TokenSourceConverter());

        string json = JsonSerializer.Serialize(tokenSource, options);

        Assert.Contains("\"kind\":\"AzureManagedIdentity\"", json);
        Assert.Contains("\"resource\":\"https://graph.microsoft.com/.default\"", json);
        Assert.Contains("\"options\":", json);
    }

    [Fact]
    // Test that a null ITokenSource will be serialized to null. 
    public void TokenSourceConverter_SerializeNull_ProducesNullJson()
    {
        ManagedIdentityTokenSource? tokenSource = null;
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TokenSourceConverter());

        string json = JsonSerializer.Serialize(tokenSource, options);

        Assert.Equal("null", json);
    }
}
