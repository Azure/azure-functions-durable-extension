// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

/// <summary>
/// the orchestration receives the host's defaultVersion from host.json (which is "2.0" in the test app).
/// </summary>
[Collection(Constants.FunctionAppCollectionName)]
public class EntityVersioningTests
{
    private readonly FunctionAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public EntityVersioningTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.TestLogs.UseTestLogger(testOutputHelper);
        _output = testOutputHelper;
    }

    /// <summary>
    /// Tests that when an entity schedules an orchestration without specifying a version,
    /// the orchestration receives the host's defaultVersion from host.json.
    /// </summary>
    [Fact]
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc
    [Trait("Python", "Skip")] // Entity versioning not implemented in Python
    [Trait("Node", "Skip")] // Entity versioning not implemented in Node
    public async Task EntityScheduledOrchestration_UsesDefaultVersion_WhenNoVersionSpecified()
    {
        // Act: Start orchestration without specifying an explicit version
        // The entity will schedule an orchestration without a version, 
        // which should receive the host's defaultVersion ("2.0" from host.json)
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "EntitySchedulesVersionedOrchestration_HttpStart");

        // Assert: Verify the request was accepted
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 60);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // The scheduled orchestration should have received the default version "2.0" from host.json
        Assert.Equal("EntityScheduledVersion: '2.0'", orchestrationDetails.Output);
    }

    /// <summary>
    /// Tests that when an entity schedules an orchestration with an explicit version
    /// that matches a configured version, that explicit version is used instead of
    /// the defaultVersion.
    /// </summary>
    [Theory]
    [InlineData("1.0")]
    [InlineData("2.0")]
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc
    [Trait("Python", "Skip")] // Entity versioning not implemented in Python
    [Trait("Node", "Skip")] // Entity versioning not implemented in Node
    public async Task EntityScheduledOrchestration_UsesExplicitVersion_WhenVersionSpecified(string explicitVersion)
    {
        // Act: Start orchestration with an explicit version
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "EntitySchedulesVersionedOrchestration_HttpStart",
            $"?explicitVersion={explicitVersion}");

        // Assert: Verify the request was accepted
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 60);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // The scheduled orchestration should have received the explicit version
        Assert.Equal($"EntityScheduledVersion: '{explicitVersion}'", orchestrationDetails.Output);
    }

    /// <summary>
    /// Tests that when an entity schedules an orchestration with an explicit version that
    /// does not match any configured version, the scheduled orchestration fails instead of
    /// silently falling back to the defaultVersion.
    /// </summary>
    [Theory]
    [InlineData("3.0")]
    [InlineData("custom-version")]
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc
    [Trait("Python", "Skip")] // Entity versioning not implemented in Python
    [Trait("Node", "Skip")] // Entity versioning not implemented in Node
    public async Task EntityScheduledOrchestration_Fails_WhenExplicitVersionIsNotConfigured(string explicitVersion)
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "EntitySchedulesVersionedOrchestration_HttpStart",
            $"?explicitVersion={explicitVersion}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 60);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        string result = orchestrationDetails.Output;
        Assert.StartsWith("FAILED: ", result);
    }

    /// <summary>
    /// Tests that when an entity schedules an orchestration with an empty string version,
    /// the defaultVersion from host.json is used.
    /// </summary>
    [Fact]
    [Trait("PowerShell", "Skip")] // Durable Entities not yet implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities not yet implemented in Java
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc
    [Trait("Python", "Skip")] // Entity versioning not implemented in Python
    [Trait("Node", "Skip")] // Entity versioning not implemented in Node
    public async Task EntityScheduledOrchestration_UsesDefaultVersion_WhenEmptyVersionSpecified()
    {
        // Act: Start orchestration with an empty string version
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            "EntitySchedulesVersionedOrchestration_HttpStart",
            "?explicitVersion=");

        // Assert: Verify the request was accepted
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 60);

        var orchestrationDetails = await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // Empty string should be treated as "no version specified" and use defaultVersion
        Assert.Equal("EntityScheduledVersion: '2.0'", orchestrationDetails.Output);
    }
}
