// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

/// <summary>
/// Helper methods for verifying client operation correlation logs in e2e tests.
/// These logs are emitted when client operations (StartOrchestration, TerminateInstance, etc.)
/// are received with a FunctionInvocationId header, enabling correlation between out-of-process
/// workers and host-side orchestration events.
/// </summary>
internal static class ClientOperationLogHelpers
{
    /// <summary>
    /// Asserts that a ClientOperationReceived log was emitted for the specified operation and instance,
    /// and that a corresponding FunctionScheduled log exists for the same instance (enabling correlation).
    /// Note: This assertion is skipped if the SDK doesn't propagate the FunctionInvocationId header yet,
    /// which happens when testing against older SDK versions that don't have this feature.
    /// </summary>
    /// <param name="logs">The collection of Core Tools logs.</param>
    /// <param name="operationType">The expected operation type (e.g., "StartOrchestration", "TerminateInstance").</param>
    /// <param name="instanceId">The expected instance ID.</param>
    public static void AssertClientOperationLogExists(
        IEnumerable<string> logs,
        string operationType,
        string instanceId)
    {
        // Log format: "Client operation '{operationType}' received for instance '{instanceId}'. FunctionInvocationId: {functionInvocationId}. ..."
        // Note: The ClientOperationReceived log is only emitted when the SDK sends the FunctionInvocationId header.
        // If the SDK doesn't support this yet (e.g., older versions), we skip the assertion.
        bool hasClientOperationLog = logs.Any(log =>
            log.Contains($"Client operation '{operationType}' received") &&
            log.Contains($"for instance '{instanceId}'"));

        // TODO: Remove this conditional check once all SDKs are released with FunctionInvocationId support.
        // Tracking issue: https://github.com/Azure/azure-functions-durable-extension/issues/3327
        if (hasClientOperationLog)
        {
            // Verify the log has a valid FunctionInvocationId (not empty)
            Assert.Contains(logs, log =>
                log.Contains($"Client operation '{operationType}' received") &&
                log.Contains($"for instance '{instanceId}'") &&
                log.Contains("FunctionInvocationId:") &&
                !log.Contains("FunctionInvocationId: ."));  // Ensure the FunctionInvocationId is not empty
        }

        // For StartOrchestration operations, also verify a function log exists for correlation
        // Note: gRPC clients emit 'started' logs (FunctionStarting), HTTP clients emit 'scheduled' logs (FunctionScheduled)
        if (operationType == "StartOrchestration")
        {
            // FunctionStarting log format: "{instanceId}: Function '{functionName} ({functionType})' started. ..."
            // FunctionScheduled log format: "{instanceId}: Function '{functionName} ({functionType})' scheduled. ..."
            Assert.Contains(logs, log =>
                log.Contains($"{instanceId}: Function '") &&
                (log.Contains("started") || log.Contains("scheduled")));
        }
    }

    /// <summary>
    /// Asserts that a ClientOperationReceived log was emitted for the specified operation type.
    /// This overload is useful when the instance ID is not known ahead of time.
    /// </summary>
    /// <param name="logs">The collection of Core Tools logs.</param>
    /// <param name="operationType">The expected operation type (e.g., "StartOrchestration", "TerminateInstance").</param>
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
}
