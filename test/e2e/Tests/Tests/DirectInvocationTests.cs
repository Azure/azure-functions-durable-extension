// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

[Collection(Constants.FunctionAppCollectionName)]
public class DirectInvocationTests
{
    private readonly FunctionAppFixture fixture;

    public DirectInvocationTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
    }

    [Theory]
    [InlineData(
        "HelloCities",
        "{\"input\":\"\"}",
        "Durable orchestrator functions do not support direct invocation. Start an orchestration from a client function by using a Durable client.")]
    [InlineData(
        "HelloCities",
        "{}",
        "Durable orchestrator functions do not support direct invocation. Start an orchestration from a client function by using a Durable client.")]
    [InlineData(
        "Counter",
        "{\"input\":\"\"}",
        "Durable entity functions do not support direct invocation. Signal an entity from a client or orchestrator function by using a Durable client.")]
    [InlineData(
        "Counter",
        "{}",
        "Durable entity functions do not support direct invocation. Signal an entity from a client or orchestrator function by using a Durable client.")]
    [Trait("PowerShell", "Skip")]
    [Trait("Python", "Skip")]
    [Trait("Node", "Skip")]
    [Trait("Java", "Skip")]
    public async Task AdminApiDirectInvocation_LogsActionableGuidance(
        string functionName,
        string requestBody,
        string expectedMessage)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeAdminFunction(functionName, requestBody);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await this.fixture.TestLogs.AssertLogExistsAsync(
            log => log.Contains($"Functions.{functionName}", StringComparison.Ordinal) &&
                log.Contains(expectedMessage, StringComparison.Ordinal),
            $"Expected the direct-invocation guidance for '{functionName}' was not found in the host logs.");
    }
}
