// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;
using Xunit.Sdk;

namespace Microsoft.Azure.Durable.Tests.DotnetIsolatedE2E;

public class ClientOperationLogHelpersTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AssertClientOperationLogCount_ExactCountForInstance_Succeeds(int expectedCount)
    {
        string[] logs = Enumerable.Repeat(CreateLog(), expectedCount)
            .Concat(new[]
            {
                CreateLog(instanceId: "rewind-instance-other"),
                CreateLog(operationType: "StartOrchestration"),
            })
            .ToArray();

        ClientOperationLogHelpers.AssertClientOperationLogCount(
            () => logs, "Rewind", "rewind-instance", expectedCount, maxWaitSeconds: 0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void AssertClientOperationLogCount_IncorrectCount_Fails(int actualCount, int expectedCount)
    {
        string[] logs = Enumerable.Repeat(CreateLog(), actualCount).ToArray();

        Assert.Throws<EqualException>(() => ClientOperationLogHelpers.AssertClientOperationLogCount(
            () => logs, "Rewind", "rewind-instance", expectedCount, maxWaitSeconds: 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-invocation-id")]
    [InlineData("abc")]
    public void AssertClientOperationLogCount_MissingOrInvalidInvocationId_Fails(string? invocationId)
    {
        string log = "Client operation 'Rewind' received for instance 'rewind-instance'.";
        if (invocationId != null)
        {
            log += $" FunctionInvocationId: {invocationId}.";
        }

        Assert.Throws<EqualException>(() => ClientOperationLogHelpers.AssertClientOperationLogCount(
            () => new[] { log }, "Rewind", "rewind-instance", 1, maxWaitSeconds: 0));
    }

    [Fact]
    public void AssertClientOperationLogCount_UnrelatedLogsOnly_Fails()
    {
        string[] logs =
        {
            CreateLog(instanceId: "rewind-instance-other"),
            CreateLog(operationType: "StartOrchestration"),
        };

        Assert.Throws<EqualException>(() => ClientOperationLogHelpers.AssertClientOperationLogCount(
            () => logs, "Rewind", "rewind-instance", 1, maxWaitSeconds: 0));
    }

    [Fact]
    public void AssertClientOperationLogCount_LogsFlushedAfterInitialSnapshot_RechecksLogs()
    {
        int polls = 0;
        ClientOperationLogHelpers.AssertClientOperationLogCount(
            () => Enumerable.Repeat(CreateLog(), Math.Min(polls++, 2)),
            "Rewind",
            "rewind-instance",
            2,
            maxWaitSeconds: 5);

        Assert.Equal(3, polls);
    }

    private static string CreateLog(string operationType = "Rewind", string instanceId = "rewind-instance")
    {
        return $"Client operation '{operationType}' received for instance '{instanceId}'. FunctionInvocationId: 01234567-89ab-cdef-0123-456789abcdef.";
    }
}
