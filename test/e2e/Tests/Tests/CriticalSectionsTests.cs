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

    /// <summary>
    /// Mutual exclusion: two instances of <c>CriticalSectionTimedLockHold</c> start
    /// concurrently and contend for the same single-entity lock. The winner acquires
    /// the lock, holds it for ~<see cref="HoldLockSeconds"/> seconds via a durable
    /// timer, then releases. The loser blocks at <c>lock(...)</c> until the winner
    /// releases, then runs its own ~<see cref="HoldLockSeconds"/>-second hold.
    ///
    /// If the locks really serialize the two instances, the slower (loser)
    /// orchestration's end-to-end elapsed time must be at least <c>2 × hold</c>.
    /// If the locks did NOT serialize (a regression), both instances would finish
    /// in ~<c>hold</c>. We assert <c>slowest &gt;= 2 × hold − slack</c>; the slack
    /// absorbs scheduling jitter and timer granularity in the DTS emulator.
    /// </summary>
    [Fact]
    [Trait("Node", "Skip")] // TODO: remove once durable-functions ships context.df.lock
    [Trait("Dotnet", "Skip")] // CriticalSection* orchestrations are only registered in the BasicNode app
    [Trait("Python", "Skip")] // context.df.lock is not implemented in Python
    [Trait("PowerShell", "Skip")] // Durable Entities are not implemented in PowerShell
    [Trait("Java", "Skip")] // Durable Entities are not implemented in Java
    [Trait("MSSQL", "Skip")] // Durable Entities are not supported in MSSQL for out-of-proc
    public async Task CriticalSection_ConcurrentLockedTransfers_Serialize()
    {
        // Must match HOLD_LOCK_SECONDS in BasicNode/src/functions/CriticalSections.ts.
        const int HoldLockSeconds = 5;
        const int SlackSeconds = 1;
        const int WaitTimeoutSeconds = 60;

        // Fire two starts in parallel so neither has a head start beyond the
        // HTTP round trip.
        Task<HttpResponseMessage> start1 = HttpHelpers.InvokeHttpTrigger(
            functionName: "StartOrchestration",
            queryString: "?orchestrationName=CriticalSectionTimedLockHold");
        Task<HttpResponseMessage> start2 = HttpHelpers.InvokeHttpTrigger(
            functionName: "StartOrchestration",
            queryString: "?orchestrationName=CriticalSectionTimedLockHold");

        HttpResponseMessage[] startResponses = await Task.WhenAll(start1, start2);
        try
        {
            Assert.Equal(HttpStatusCode.Accepted, startResponses[0].StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, startResponses[1].StatusCode);

            string statusUri1 = await DurableHelpers.ParseStatusQueryGetUriAsync(startResponses[0]);
            string statusUri2 = await DurableHelpers.ParseStatusQueryGetUriAsync(startResponses[1]);

            // Wait for both to complete in parallel.
            await Task.WhenAll(
                DurableHelpers.WaitForOrchestrationStateAsync(statusUri1, "Completed", WaitTimeoutSeconds),
                DurableHelpers.WaitForOrchestrationStateAsync(statusUri2, "Completed", WaitTimeoutSeconds));

            DurableHelpers.OrchestrationStatusDetails details1 =
                await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusUri1);
            DurableHelpers.OrchestrationStatusDetails details2 =
                await DurableHelpers.GetRunningOrchestrationDetailsAsync(statusUri2);

            Assert.NotEqual(details1.InstanceId, details2.InstanceId);
            Assert.Equal("\"held\"", details1.Output);
            Assert.Equal("\"held\"", details2.Output);

            TimeSpan elapsed1 = details1.LastUpdatedTime - details1.CreatedTime;
            TimeSpan elapsed2 = details2.LastUpdatedTime - details2.CreatedTime;
            TimeSpan slowest = elapsed1 > elapsed2 ? elapsed1 : elapsed2;

            this.output.WriteLine(
                $"Instance 1: elapsed={elapsed1.TotalSeconds:F2}s; Instance 2: elapsed={elapsed2.TotalSeconds:F2}s; slowest={slowest.TotalSeconds:F2}s");

            // If the locks serialize the two instances, the loser cannot finish in
            // less than ~2 × hold seconds. Without lock serialization, both would
            // finish in ~hold seconds and this assertion would fail.
            double minSlowestSeconds = (2 * HoldLockSeconds) - SlackSeconds;
            Assert.True(
                slowest.TotalSeconds >= minSlowestSeconds,
                $"Expected slower instance to take >= {minSlowestSeconds}s (mutual exclusion), but it took {slowest.TotalSeconds:F2}s. " +
                $"Instance 1: {elapsed1.TotalSeconds:F2}s, Instance 2: {elapsed2.TotalSeconds:F2}s.");
        }
        finally
        {
            startResponses[0].Dispose();
            startResponses[1].Dispose();
        }
    }
}
