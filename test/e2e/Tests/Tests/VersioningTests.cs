// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class VersioningTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public VersioningTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    [Theory]
    [InlineData(null)] // Represents a non-versioned case.
    [InlineData("")] // Empty version case - default behavior
    [InlineData("1.0")]
    [InlineData("2.0")]
    [Trait("PowerShell", "Skip")] // Intentional orchestration versioning not yet implemented in PowerShell.
                                  // This test can be implemented using default versions but will require the
                                  // testing framework to implement host.json modifications and host restarts
                                  // mid-test.
    public async Task TestVersionedOrchestration_OKWithMatchingVersion(string? version)
    {
        string queryString = version == null ? string.Empty : $"?version={version}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("OrchestrationVersion_HttpStart", queryString);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        if (version != null)
        {
            Assert.Equal($"Version: '{version}'", orchestrationDetails.Output);
        }
        else
        {
            // The default version (2.0) from the host.json file should've been used here.
            Assert.Equal("Version: '2.0'", orchestrationDetails.Output);
        }
    }

    [Theory]
    [InlineData(null)] // Represents a non-versioned case.
    [InlineData("")] // Non-versioned/empty-versioned case.
    [InlineData("1.0")]
    [InlineData("2.0")]
    [Trait("PowerShell", "Skip")] // See notes on first test.
    public async Task TestVersionedSubOrchestration_OKWithMatchingVersion(string? subOrchestrationVersion)
    {
        string queryString = subOrchestrationVersion == null ? string.Empty : $"?subOrchestrationVersion={subOrchestrationVersion}";
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("OrchestrationSubVersion_HttpStart", queryString);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);
        if (subOrchestrationVersion != null)
        {
            Assert.Equal($"Parent Version: '2.0' | Sub Version: '{subOrchestrationVersion}'", orchestrationDetails.Output);
        }
        else
        {
            // The default version (2.0) from the host.json file should've been used here.
            Assert.Equal("Parent Version: '2.0' | Sub Version: '2.0'", orchestrationDetails.Output);
        }
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // See notes on first test.
    public async Task TestVersionedOrchestration_FailsWithNonMatchingVersion()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("OrchestrationVersion_HttpStart", $"?version=3.0");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 30);
    }
}
