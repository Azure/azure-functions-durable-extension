using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionSequentialName)]
public class PurgeInstancesTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public PurgeInstancesTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
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
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
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
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
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
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
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
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    public async Task PurgeOrchestrationHistoryAfterInvocation_Succeeds()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");
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
    [Trait("PowerShell", "Skip")] // Instance purging not supported in PowerShell
    public async Task PurgeAfterPurge_ZeroRows()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=HelloCities");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

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
