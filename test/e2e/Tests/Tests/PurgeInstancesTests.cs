using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class PurgeInstancesTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PurgeInstancesTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    [Fact]
    public async Task PurgeOrchestrationHistory_StartAndEnd_Succeeds()
    {
        DateTime purgeStartTime = DateTime.MinValue;
        DateTime purgeEndTime = DateTime.UtcNow;
        string queryParams = $"?purgeStartTime={purgeStartTime:o}&purgeEndTime={purgeEndTime:o}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PurgeOrchestrationHistory_Start_Succeeds()
    {
        DateTime purgeStartTime = DateTime.MinValue;
        DateTime purgeEndTime = DateTime.UtcNow;
        string queryParams = $"?purgeStartTime={purgeStartTime:o}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    public async Task PurgeOrchestrationHistory_End_Succeeds()
    {
        DateTime purgeStartTime = DateTime.MinValue;
        DateTime purgeEndTime = DateTime.UtcNow;
        string queryParams = $"?purgeEndTime={purgeEndTime:o}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    public async Task PurgeOrchestrationHistory_NoBoundaries_Succeeds()
    {
        DateTime purgeStartTime = DateTime.MinValue;
        DateTime purgeEndTime = DateTime.UtcNow;
        string queryParams = $"";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", queryParams);
        string actualMessage = await response.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", actualMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    public async Task PurgeOrchestrationHistoryAfterInvocation_Succeeds()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("HelloCities_HttpStart", "");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        DateTime purgeEndTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage purgeResponse = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?purgeEndTime={purgeEndTime:o}");
        string purgeMessage = await purgeResponse.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", purgeMessage);
        Assert.DoesNotMatch(@"^Purged 0 records$", purgeMessage);
        Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);
    }

    [Fact]
    [Trait("DTS", "Skip")] // Skip this test as there is a bug with current DTS backend, the createdTimeTo couldn't be null. 
    public async Task PurgeAfterPurge_ZeroRows()
    {
        DateTime purgeEndTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        using HttpResponseMessage purgeResponse = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?purgeEndTime={purgeEndTime:o}");
        string purgeMessage = await purgeResponse.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged [0-9]* records$", purgeMessage);
        using HttpResponseMessage purgeAgainResponse = await HttpHelpers.InvokeHttpTrigger("PurgeOrchestrationHistory", $"?purgeEndTime={purgeEndTime:o}");
        string purgeAgainMessage = await purgeAgainResponse.Content.ReadAsStringAsync();
        Assert.Matches(@"^Purged 0 records$", purgeAgainMessage);
        Assert.Equal(HttpStatusCode.OK, purgeAgainResponse.StatusCode);
    }
}
