// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

/// <summary>
/// Unit tests for <see cref="DurableTaskClientExtensions"/>.
/// Tests focus on the GetBaseUrl method which handles forwarded headers
/// for proxy and application gateway scenarios.
/// </summary>
public class DurableTaskClientExtensionsTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a mock HttpRequestData with the specified URL and headers.
    /// </summary>
    private static HttpRequestData CreateMockRequest(
        string url,
        Dictionary<string, IEnumerable<string>>? headers = null)
    {
        var uri = new Uri(url);
        var functionContext = new Mock<FunctionContext>();

        var request = new Mock<HttpRequestData>(functionContext.Object);
        request.Setup(r => r.Url).Returns(uri);

        var httpHeaders = new HttpHeadersCollection();
        if (headers != null)
        {
            foreach (var kvp in headers)
            {
                foreach (var value in kvp.Value)
                {
                    httpHeaders.Add(kvp.Key, value);
                }
            }
        }

        request.Setup(r => r.Headers).Returns(httpHeaders);

        return request.Object;
    }

    #endregion

    #region Fallback Behavior Tests

    [Fact]
    public void GetBaseUrl_NoForwardedHeaders_ReturnsOriginalUrl()
    {
        // Arrange
        var request = CreateMockRequest("https://localhost:7071/api/test");

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://localhost:7071", result);
    }

    [Fact]
    public void GetBaseUrl_NoForwardedHeaders_HttpScheme_ReturnsOriginalUrl()
    {
        // Arrange
        var request = CreateMockRequest("http://example.com/api/test");

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("http://example.com", result);
    }

    [Fact]
    public void GetBaseUrl_EmptyForwardedHeader_ReturnsOriginalUrl()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "" } }
        };
        var request = CreateMockRequest("https://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://localhost:7071", result);
    }

    #endregion

    #region Standard Forwarded Header Tests (RFC 7239)

    [Fact]
    public void GetBaseUrl_ForwardedHeader_HostAndProto_ReturnsForwardedUrl()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=api.example.com;proto=https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_HostOnly_UsesOriginalScheme()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=api.example.com" } }
        };
        var request = CreateMockRequest("https://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_WithPort_ReturnsHostWithPort()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=api.example.com:8443;proto=https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com:8443", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_QuotedValues_HandlesCorrectly()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=\"api.example.com\";proto=\"https\"" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_MultipleProxies_UsesFirstEntry()
    {
        // Arrange - Multiple proxies are separated by commas, first is original client
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=original.example.com;proto=https, host=proxy.internal.com;proto=http" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://original.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_CaseInsensitive()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "HOST=api.example.com;PROTO=HTTPS" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_WithForDirective_IgnoresFor()
    {
        // Arrange - "for" directive contains client IP, should be ignored
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "for=192.168.1.1;host=api.example.com;proto=https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    #endregion

    #region X-Forwarded-* Header Tests

    [Fact]
    public void GetBaseUrl_XForwardedHeaders_HostAndProto_ReturnsForwardedUrl()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_XForwardedHost_OnlyHost_UsesOriginalScheme()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com" } }
        };
        var request = CreateMockRequest("https://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_XForwardedHeaders_WithPort_ReturnsHostWithPort()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com:8443" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com:8443", result);
    }

    [Fact]
    public void GetBaseUrl_XForwardedHeaders_MultipleValues_UsesFirst()
    {
        // Arrange - Multiple hosts separated by comma
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "original.example.com, proxy.internal.com" } },
            { "X-Forwarded-Proto", new[] { "https, http" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://original.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_XForwardedHeaders_MultipleHeaderValues_UsesFirst()
    {
        // Arrange - Multiple header entries
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "first.example.com", "second.example.com" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://first.example.com", result);
    }

    #endregion

    #region Header Priority Tests

    [Fact]
    public void GetBaseUrl_BothForwardedAndXForwarded_PrefersForwardedHeader()
    {
        // Arrange - Forwarded header should take precedence
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=forwarded.example.com;proto=https" } },
            { "X-Forwarded-Host", new[] { "xforwarded.example.com" } },
            { "X-Forwarded-Proto", new[] { "http" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://forwarded.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_InvalidForwarded_FallsBackToXForwarded()
    {
        // Arrange - Invalid Forwarded header should fallback to X-Forwarded-*
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "invalid-header-format" } },
            { "X-Forwarded-Host", new[] { "xforwarded.example.com" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://xforwarded.example.com", result);
    }

    #endregion

    #region Security Tests - Host Injection Prevention

    [Theory]
    [InlineData("evil.com/path")]          // Path injection
    [InlineData("evil.com?query=value")]   // Query injection
    [InlineData("evil.com#fragment")]      // Fragment injection
    [InlineData("evil.com\\path")]         // Backslash path injection
    [InlineData("evil.com@trusted.com")]   // Authority injection attempt
    [InlineData("<script>alert(1)</script>")] // XSS attempt
    [InlineData("evil%2Ecom")]             // URL encoded injection
    [InlineData("")]                        // Empty string
    [InlineData("   ")]                     // Whitespace only
    public void GetBaseUrl_XForwardedHost_MaliciousHost_FallsBackToOriginal(string maliciousHost)
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { maliciousHost } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should fall back to original URL due to invalid host
        Assert.Equal("http://localhost:7071", result);
    }

    // Note: Header injection with newlines (e.g., "evil.com\r\nX-Injected: header") 
    // is automatically prevented by the HTTP headers collection at the framework level,
    // which rejects values containing newline characters.

    [Theory]
    [InlineData("host=evil.com/path")]
    [InlineData("host=evil.com?query")]
    [InlineData("host=<script>")]
    public void GetBaseUrl_ForwardedHeader_MaliciousHost_FallsBackToOriginal(string maliciousForwarded)
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { maliciousForwarded } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should fall back to original URL
        Assert.Equal("http://localhost:7071", result);
    }

    #endregion

    #region Security Tests - Protocol Validation

    [Theory]
    [InlineData("javascript")]    // XSS protocol
    [InlineData("data")]          // Data protocol
    [InlineData("file")]          // File protocol
    [InlineData("ftp")]           // FTP protocol
    [InlineData("//")]            // Protocol-relative
    [InlineData("https://evil.com")] // Full URL injection
    public void GetBaseUrl_XForwardedProto_InvalidProtocol_UsesOriginalScheme(string maliciousProto)
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com" } },
            { "X-Forwarded-Proto", new[] { maliciousProto } }
        };
        var request = CreateMockRequest("https://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should use https from original request, not the malicious proto
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_XForwardedProto_EmptyProtocol_UsesOriginalScheme()
    {
        // Arrange - Empty protocol should fall back to original scheme
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com" } },
            { "X-Forwarded-Proto", new[] { "" } }
        };
        var request = CreateMockRequest("https://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should use https from original request
        Assert.Equal("https://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_XForwardedProto_WhitespaceOnlyProtocol_UsesOriginalScheme()
    {
        // Arrange - Whitespace-only protocol should fall back to original scheme
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com" } },
            { "X-Forwarded-Proto", new[] { "   " } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should use http from original request
        Assert.Equal("http://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_NoProto_UsesOriginalScheme()
    {
        // Arrange - Forwarded header with host only, no proto directive
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=api.example.com" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should use http from original request since no proto specified
        Assert.Equal("http://api.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_EmptyProto_UsesOriginalScheme()
    {
        // Arrange - Forwarded header with empty proto value
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "host=api.example.com;proto=" } }
        };
        var request = CreateMockRequest("https://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should use https from original request since proto is empty
        Assert.Equal("https://api.example.com", result);
    }

    [Theory]
    [InlineData("http", "http")]
    [InlineData("https", "https")]
    [InlineData("HTTP", "http")]
    [InlineData("HTTPS", "https")]
    [InlineData("Http", "http")]
    [InlineData("Https", "https")]
    public void GetBaseUrl_XForwardedProto_ValidProtocols_ReturnsNormalized(string proto, string expected)
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com" } },
            { "X-Forwarded-Proto", new[] { proto } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal($"{expected}://api.example.com", result);
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    public void GetBaseUrl_AzureApplicationGateway_ReturnsCorrectUrl()
    {
        // Arrange - Azure Application Gateway typically sets these headers
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "myapp.azurewebsites.net" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://10.0.0.5:80/api/orchestrators/MyOrchestrator", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://myapp.azurewebsites.net", result);
    }

    [Fact]
    public void GetBaseUrl_AzureFrontDoor_ReturnsCorrectUrl()
    {
        // Arrange - Azure Front Door scenario
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "myapi.azurefd.net" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://myapp-backend.azurewebsites.net/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://myapi.azurefd.net", result);
    }

    [Fact]
    public void GetBaseUrl_NginxProxy_WithStandardForwarded_ReturnsCorrectUrl()
    {
        // Arrange - nginx with RFC 7239 Forwarded header
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "for=192.168.1.100;host=public.example.com;proto=https" } }
        };
        var request = CreateMockRequest("http://internal-service:8080/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://public.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_KubernetesIngress_ReturnsCorrectUrl()
    {
        // Arrange - Kubernetes Ingress Controller scenario
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.mycompany.com" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://durable-functions-svc.default.svc.cluster.local:80/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.mycompany.com", result);
    }

    [Fact]
    public void GetBaseUrl_LocalDevelopment_NoHeaders_ReturnsLocalhost()
    {
        // Arrange - Local development without proxy
        var request = CreateMockRequest("http://localhost:7071/api/test");

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("http://localhost:7071", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetBaseUrl_IPv6Host_ReturnsCorrectUrl()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "[::1]:8080" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://[::1]:8080", result);
    }

    [Fact]
    public void GetBaseUrl_IPv6Host_NoPort_ReturnsCorrectUrl()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "[::1]" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://[::1]", result);
    }

    [Fact]
    public void GetBaseUrl_HostWithPort80_ReturnsCorrectUrl()
    {
        // Arrange - Standard HTTP port explicitly specified
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com:80" } },
            { "X-Forwarded-Proto", new[] { "http" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("http://api.example.com:80", result);
    }

    [Fact]
    public void GetBaseUrl_HostWithPort443_ReturnsCorrectUrl()
    {
        // Arrange - Standard HTTPS port explicitly specified
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api.example.com:443" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com:443", result);
    }

    [Fact]
    public void GetBaseUrl_InternationalizedDomainName_ReturnsCorrectUrl()
    {
        // Arrange - IDN domain (Punycode representation)
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "xn--n3h.example.com" } },  // Punycode for emoji domain
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://xn--n3h.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_SubdomainWithNumbers_ReturnsCorrectUrl()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Host", new[] { "api-v2.123-test.example.com" } },
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api-v2.123-test.example.com", result);
    }

    [Fact]
    public void GetBaseUrl_XForwardedProto_OnlyProtoNoHost_ReturnsOriginalUrl()
    {
        // Arrange - Only proto header, no host - should ignore
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Forwarded-Proto", new[] { "https" } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert - Should return original URL since we need a host
        Assert.Equal("http://localhost:7071", result);
    }

    [Fact]
    public void GetBaseUrl_ForwardedHeader_WhitespaceHandling()
    {
        // Arrange - Header with extra whitespace around directives (but not around =)
        // Note: Whitespace directly around the = sign is not standard and is not supported
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Forwarded", new[] { "  host=api.example.com ; proto=https  " } }
        };
        var request = CreateMockRequest("http://localhost:7071/api/test", headers);

        // Act
        string result = DurableTaskClientExtensions.GetBaseUrl(request);

        // Assert
        Assert.Equal("https://api.example.com", result);
    }

    #endregion
}
