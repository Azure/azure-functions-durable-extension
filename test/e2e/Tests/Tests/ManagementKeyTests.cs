// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Web;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(ManagementKeyCollection.Name)]
[Trait("PowerShell", "Skip")] // Header-based management opt-in is covered only by .NET for now.
[Trait("Python", "Skip")] // Header-based management opt-in is covered only by .NET for now.
[Trait("Node", "Skip")] // Header-based management opt-in is covered only by .NET for now.
[Trait("Java", "Skip")] // Header-based management opt-in is covered only by .NET for now.
public class ManagementKeyTests
{
    private readonly ManagementKeyFixture fixture;

    public ManagementKeyTests(ManagementKeyFixture fixture, ITestOutputHelper output)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(output);
    }

    [Fact]
    public async Task ExcludeManagementKeyFromUrls_RequiresHeaderAuthentication()
    {
        using HttpResponseMessage start = await HttpHelpers.InvokeHttpTrigger(
            "StartOrchestration", "?orchestrationName=HelloCities");
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        JObject payload = JObject.Parse(await start.Content.ReadAsStringAsync());
        Assert.All(payload.Properties(), property => Assert.Equal(JTokenType.String, property.Value.Type));
        Assert.Null(payload.GetValue("requiredHeaders", StringComparison.OrdinalIgnoreCase));

        JProperty[] links = payload.Properties()
            .Where(property => property.Name.EndsWith("Uri", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(links);
        foreach (JProperty link in links)
        {
            var uri = new Uri(link.Value.Value<string>()!);
            Assert.Equal(new Uri(Constants.FunctionsHostUrl).GetLeftPart(UriPartial.Authority),
                uri.GetLeftPart(UriPartial.Authority));
            AssertKeyFree(uri);
        }

        string statusUrl = payload.GetValue("statusQueryGetUri", StringComparison.OrdinalIgnoreCase)!.Value<string>()!;
        Assert.NotNull(start.Headers.Location);
        Assert.Equal(statusUrl, start.Headers.Location.OriginalString);
        AssertKeyFree(start.Headers.Location);

        using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        using HttpResponseMessage missingKey = await httpClient.GetAsync(statusUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, missingKey.StatusCode);
        using HttpRequestMessage invalidRequest = CreateRequest(HttpMethod.Get, statusUrl, "invalid-test-key");
        using HttpResponseMessage invalidKey = await httpClient.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidKey.StatusCode);

        JObject? completed = null;
        await TestUtility.RetryAsync(async () =>
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, statusUrl, this.fixture.SystemKey);
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted,
                $"Header-authenticated status request returned {response.StatusCode}.");
            if (response.Headers.Location != null)
            {
                AssertKeyFree(response.Headers.Location);
            }

            JObject status = JObject.Parse(await response.Content.ReadAsStringAsync());
            string? runtimeStatus = status.Value<string>("runtimeStatus");
            Assert.DoesNotContain(runtimeStatus, new[] { "Failed", "Terminated", "Canceled" });
            if (runtimeStatus == "Completed")
            {
                completed = status;
                return true;
            }

            return false;
        }, timeout: 60000);
        Assert.NotNull(completed);
        Assert.Contains("Hello Tokyo!", completed["output"]!.ToString());

        string purgeUrl = payload.GetValue("purgeHistoryDeleteUri", StringComparison.OrdinalIgnoreCase)!.Value<string>()!;
        using HttpRequestMessage purgeRequest = CreateRequest(HttpMethod.Delete, purgeUrl, this.fixture.SystemKey);
        using HttpResponseMessage purgeResponse = await httpClient.SendAsync(purgeRequest);
        Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);
    }

    private static void AssertKeyFree(Uri uri)
    {
        string?[] names = HttpUtility.ParseQueryString(uri.Query).AllKeys;
        Assert.False(names.Any(name => string.Equals(name, "code", StringComparison.OrdinalIgnoreCase)),
            "Management URL must not contain a code query parameter.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string key)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("x-functions-key", key);
        return request;
    }
}
