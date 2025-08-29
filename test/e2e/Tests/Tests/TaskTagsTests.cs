// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit.Abstractions;
using Xunit;
using System.Diagnostics;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class TaskTagsTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public TaskTagsTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    // Due to some kind of asynchronous race condition in XUnit, when running these tests in pipelines,
    // the output may be disposed before the message is written. Just ignore these types of errors for now. 
    private void WriteOutput(string message)
    {
        try
        {
            this.output.WriteLine(message);
        }
        catch
        {
            // Ignore
        }
    }

    [Fact]
    [Trait("PowerShell", "Skip")] // Tags are not currently implemented in PowerShell
    [Trait("Python", "Skip")] // Tags are not currently implemented in Python
    [Trait("Node", "Skip")] // Tags are not currently implemented in Node
    public async Task RunOrchestrationWithTags()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=TaskTags&tagKey=key1&tagValue=value1");

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);
        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        Assert.NotNull(orchestrationDetails?.Tags);
        Assert.Contains("key1", orchestrationDetails.Tags.Keys);
        Assert.Equal("value1", orchestrationDetails.Tags["key1"]);

        // TODO: Verify activity has tags.
    }
}