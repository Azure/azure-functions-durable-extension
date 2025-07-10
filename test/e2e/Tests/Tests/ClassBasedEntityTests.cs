// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class ClassBasedEntityTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public ClassBasedEntityTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    [Fact]
    public async Task ClassBasedEntityTest()
    {
        // Start the orchestration that invokes the class-based entity
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartClassBasedEntityOrchestration");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        // The output of the orchestration should contain the expected string that contains the entity's state.
        // The entity state is a simple string that shows whether the injected services are available.
        DurableHelpers.OrchestrationStatusDetails orchestrationDetails =
            await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        this.output.WriteLine(
            $"Orchestration {orchestrationDetails.RuntimeStatus}. Output = {orchestrationDetails.Output}");

        string expectedOutput = "IConfiguration: yes, MyInjectedService: yes, BlobContainerClient: yes";
        Assert.Equal(expectedOutput, orchestrationDetails.Output);
    }
}
