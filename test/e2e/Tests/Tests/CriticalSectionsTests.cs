// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

/// <summary>
/// E2E coverage for OOProc critical sections (entity locks).
///
/// These tests exercise the new <c>LockEntities</c> / <c>ReleaseEntities</c>
/// action types and OOProc schema V4 that this extension change adds.
///
/// Today every test is skipped for every language. Reason matrix:
///   - Node: the extension half is in this repo, but the JS worker change
///     (durable-functions PR) has not been
///     released. Remove <c>[Trait("Node", "Skip")]</c> once `durable-functions`
///     ships the <c>context.df.lock</c> API.
///   - Dotnet: the test orchestrations (CriticalSectionLockedTransfer,
///     CriticalSectionNestedLockViolation) are only registered in the
///     BasicNode app — running them against BasicDotNetIsolated would 404.
///   - Python, PowerShell, Java: <c>context.df.lock</c> / equivalent is not
///     implemented in those OOProc workers yet.
///   - MSSQL backend: entities are not supported in MSSQL for out-of-proc
///     (https://github.com/microsoft/durabletask-mssql/issues/205).
/// </summary>
[Collection(Constants.FunctionAppCollectionName)]
public class CriticalSectionsTests
{
    private readonly FunctionAppFixture fixture;
    private readonly ITestOutputHelper output;

    public CriticalSectionsTests(FunctionAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this.fixture = fixture;
        this.fixture.TestLogs.UseTestLogger(testOutputHelper);
        this.output = testOutputHelper;
    }

    /// <summary>
    /// Happy-path: orchestration acquires both locks, transfers 30 from A (seeded 100)
    /// to B (seeded 0), releases, and reports the post-commit balances.
    /// </summary>
    [Fact]
    [Trait("Node", "Skip")] // TODO: remove once durable-functions ships context.df.lock
    [Trait("Dotnet", "Skip")] // CriticalSection* orchestrations are only registered in the BasicNode app
    [Trait("Python", "Skip")] // context.df.lock is not implemented in Python
    [Trait("PowerShell", "Skip")] // Durable Entities are not implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities are not implemented in Java
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc (see https://github.com/microsoft/durabletask-mssql/issues/205)
    public async Task CriticalSection_LockedTransfer_Succeeds()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            functionName: "StartOrchestration",
            queryString: "?orchestrationName=CriticalSectionLockedTransfer");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Completed", 60);

        DurableHelpers.OrchestrationStatusDetails orchestrationDetails =
            await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // The orchestration seeds A=100, B=0 and transfers 30, so the post-commit balances
        // must be exactly A=70, B=30. Quoted because the JSON output is a string.
        Assert.Equal("\"from=70;to=30\"", orchestrationDetails.Output);
    }

    /// <summary>
    /// Rule enforcement: a nested <c>lock</c> call inside an active critical
    /// section must throw <c>LockingRulesViolationError</c> and fail the
    /// orchestration.
    /// </summary>
    [Fact]
    [Trait("Node", "Skip")] // TODO: remove once durable-functions ships context.df.lock
    [Trait("Dotnet", "Skip")] // CriticalSection* orchestrations are only registered in the BasicNode app
    [Trait("Python", "Skip")] // context.df.lock is not implemented in Python
    [Trait("PowerShell", "Skip")] // Durable Entities are not implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities are not implemented in Java
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc
    public async Task CriticalSection_NestedLock_FailsOrchestration()
    {
        using HttpResponseMessage response = await HttpHelpers.InvokeHttpTrigger(
            functionName: "StartOrchestration",
            queryString: "?orchestrationName=CriticalSectionNestedLockViolation");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string statusQueryGetUri = await DurableHelpers.ParseStatusQueryGetUriAsync(response);
        await DurableHelpers.WaitForOrchestrationStateAsync(statusQueryGetUri, "Failed", 60);

        DurableHelpers.OrchestrationStatusDetails orchestrationDetails =
            await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusQueryGetUri);

        // The failure payload must mention the locking-rule violation. We assert
        // on a stable substring; the full message is worker-defined and may change.
        Assert.Contains("critical section", orchestrationDetails.Output, StringComparison.OrdinalIgnoreCase);
    }
}
