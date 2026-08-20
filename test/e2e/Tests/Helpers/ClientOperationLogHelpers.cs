// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

/// <summary>
/// Helper methods for verifying client operation correlation logs in e2e tests.
/// These logs are emitted when client operations (StartOrchestration, Terminate, etc.)
/// are received with a FunctionInvocationId header, enabling correlation between out-of-process
/// workers and host-side orchestration events.
/// </summary>
internal static class ClientOperationLogHelpers
{
    /// <summary>
    /// Waits for Core Tools logs to be flushed, then asserts that a ClientOperationReceived log
    /// was emitted for the specified operation and instance. Retries with polling to avoid flaky
    /// tests caused by log flush timing.
    /// </summary>
    /// <param name="getLogs">A function that returns the current collection of Core Tools logs (re-evaluated on each poll).</param>
    /// <param name="operationType">The expected operation type (e.g., "StartOrchestration", "Terminate").</param>
    /// <param name="instanceId">The expected instance ID.</param>
    /// <param name="languageType">The language type of the function app under test. Non-DotnetIsolated languages skip polling.</param>
    /// <param name="maxWaitSeconds">Maximum time to wait for the log to appear (default: 5 seconds).</param>
    public static void AssertClientOperationLogExists(
        Func<IEnumerable<string>> getLogs,
        string operationType,
        string instanceId,
        LanguageType languageType,
        int maxWaitSeconds = 5)
    {
        // TODO: Evaluate whether WaitForCondition can be replaced with the new TestLoggerProvider.AssertLogExistsAsync 
        // method, which provides similar polling functionality with xUnit assertions and better failure messages. This
        // would require updating the method signature to async and changing the parameter to accept the TestLoggerProvider 
        // directly instead of a log collection function.

        // Only the .NET isolated worker SDK currently emits the FunctionInvocationId header.
        // Skip entirely for other languages to avoid unnecessary test delays.
        // Tracking issue: https://github.com/Azure/azure-functions-durable-extension/issues/3327
        if (languageType != LanguageType.DotnetIsolated)
        {
            return;
        }

        // Poll for the log to appear, giving Core Tools time to flush logs.
        // Re-fetch logs on each iteration since CoreToolsLogs returns a snapshot.
        bool hasClientOperationLog = WaitForCondition(
            () => getLogs().Any(log =>
                log.Contains($"Client operation '{operationType}' received") &&
                log.Contains($"for instance '{instanceId}'")),
            maxWaitSeconds);

        Assert.True(hasClientOperationLog,
            $"Expected ClientOperationReceived log for '{operationType}' on instance '{instanceId}' was not found.");

        // Verify the log has a valid FunctionInvocationId (not empty)
        Assert.Contains(getLogs(), log =>
            log.Contains($"Client operation '{operationType}' received") &&
            log.Contains($"for instance '{instanceId}'") &&
            log.Contains("FunctionInvocationId:") &&
            !log.Contains("FunctionInvocationId: ."));  // Ensure the FunctionInvocationId is not empty

        // For StartOrchestration operations, also verify a function log exists for correlation
        // Note: gRPC clients emit 'started' logs (FunctionStarting), HTTP clients emit 'scheduled' logs (FunctionScheduled)
        if (operationType == "StartOrchestration")
        {
            // FunctionStarting log format: "{instanceId}: Function '{functionName} ({functionType})' started. ..."
            // FunctionScheduled log format: "{instanceId}: Function '{functionName} ({functionType})' scheduled. ..."
            Assert.Contains(getLogs(), log =>
                log.Contains($"{instanceId}: Function '") &&
                (log.Contains("started") || log.Contains("scheduled")));
        }
    }

    /// <summary>
    /// Asserts that a ClientOperationReceived log was emitted for the specified operation type.
    /// This overload is useful when the instance ID is not known ahead of time.
    /// </summary>
    /// <param name="logs">The collection of Core Tools logs.</param>
    /// <param name="operationType">The expected operation type (e.g., "StartOrchestration", "Terminate").</param>
    public static void AssertClientOperationLogExists(
        IEnumerable<string> logs,
        string operationType)
    {
        // Log format: "Client operation '{operationType}' received for instance '{instanceId}'. FunctionInvocationId: {functionInvocationId}. ..."
        Assert.Contains(logs, log =>
            log.Contains($"Client operation '{operationType}' received") &&
            log.Contains("FunctionInvocationId:") &&
            !log.Contains("FunctionInvocationId: ."));  // Ensure the FunctionInvocationId is not empty
    }

    /// <summary>
    /// Extracts the FunctionInvocationId from a ClientOperationReceived log entry.
    /// </summary>
    /// <param name="log">The log entry to extract from.</param>
    /// <returns>The extracted FunctionInvocationId, or null if not found.</returns>
    public static string? ExtractFunctionInvocationId(string log)
    {
        // Log format: "... FunctionInvocationId: {functionInvocationId}. HubName: ..."
        var match = Regex.Match(log, @"FunctionInvocationId:\s*([a-f0-9\-]+)\.", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Checks if any ClientOperationReceived log exists (without asserting).
    /// Useful for scenarios where logging is optional or to determine if correlation is working.
    /// </summary>
    /// <param name="logs">The collection of Core Tools logs.</param>
    /// <param name="operationType">The expected operation type.</param>
    /// <returns>True if a matching log exists, false otherwise.</returns>
    public static bool HasClientOperationLog(
        IEnumerable<string> logs,
        string operationType)
    {
        return logs.Any(log =>
            log.Contains($"Client operation '{operationType}' received") &&
            log.Contains("FunctionInvocationId:") &&
            !log.Contains("FunctionInvocationId: ."));
    }

    /// <summary>
    /// Gets the count of ClientOperationReceived logs for a specific operation type.
    /// </summary>
    /// <param name="logs">The collection of Core Tools logs.</param>
    /// <param name="operationType">The operation type to count.</param>
    /// <returns>The number of matching logs.</returns>
    public static int GetClientOperationLogCount(
        IEnumerable<string> logs,
        string operationType)
    {
        return logs.Count(log =>
            log.Contains($"Client operation '{operationType}' received") &&
            log.Contains("FunctionInvocationId:"));
    }

    /// <summary>
    /// Gets the count of ClientOperationReceived logs for a specific operation type and instance ID.
    /// </summary>
    /// <param name="logs">The collection of Core Tools logs.</param>
    /// <param name="operationType">The operation type to count.</param>
    /// <param name="instanceId">The instance ID to filter by.</param>
    /// <returns>The number of matching logs.</returns>
    public static int GetClientOperationLogCount(
        IEnumerable<string> logs,
        string operationType,
        string instanceId)
    {
        return logs.Count(log =>
            log.Contains($"Client operation '{operationType}' received") &&
            log.Contains($"for instance '{instanceId}'") &&
            log.Contains("FunctionInvocationId:"));
    }

    /// <summary>
    /// Polls a condition with a 250ms interval, returning true if it becomes true within the timeout.
    /// </summary>
    private static bool WaitForCondition(Func<bool> condition, int maxWaitSeconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return condition();
    }
}
