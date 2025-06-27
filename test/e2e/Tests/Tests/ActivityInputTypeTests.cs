// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class ActivityInputTypeTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public ActivityInputTypeTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    // This test verifies that different types of inputs can be properly serialized and passed to activity functions.
    [Fact]
    [Trait("PowerShell", "Skip")] // Test not yet implemented in PowerShell
    public async Task DifferentActivityInputTypeTests()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger("StartOrchestration", "?orchestrationName=ActivityInputTypeOrchestrator");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);

        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 30);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // Verify that all activity functions can successfully receive their corresponding input types:
        // - byte[]
        // - empty byte[]
        // - single byte
        // - custom class (with byte[] property)
        // - int[]
        // - string
        // - custom class array
        // This especially verifies that byte[] serialization works correctly without any errors
        Assert.Contains("Received byte[]: [1, 2, 3, 4, 5]", orchestrationDetails.Output);
        Assert.Contains("Received byte[]: []", orchestrationDetails.Output);
        Assert.Contains("Received byte: 42", orchestrationDetails.Output);
        Assert.Contains("Received CustomClass: {Name: Test, Age: 25, Duration: 01:00:00, Data: [1, 2, 3]}", orchestrationDetails.Output);
        Assert.Contains("Received int[]: [1, 2, 3, 4, 5]", orchestrationDetails.Output);
        Assert.Contains("Received string: Test string input", orchestrationDetails.Output);
        Assert.Contains("Received CustomClass[]: [{Name: Test1, Age: 25, Duration: 00:30:00, Data: [1, 2, 3]}, {Name: Test2, Age: 30, Duration: 00:45:00, Data: []}]", orchestrationDetails.Output);

        // Verify there were no serialization errors, especially for byte[] types
        Assert.DoesNotContain("Error:", orchestrationDetails.Output);
    }
}
