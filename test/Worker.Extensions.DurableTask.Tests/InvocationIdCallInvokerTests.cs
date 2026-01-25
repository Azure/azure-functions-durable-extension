// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Grpc.Core;
using Xunit;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Tests;

public class InvocationIdCallInvokerTests
{
    [Fact]
    public void SetCurrentInvocationId_SetsValueInAsyncLocal()
    {
        // Arrange
        const string expectedId = "test-invocation-id-123";

        // Act
        InvocationIdCallInvoker.SetCurrentInvocationId(expectedId);
        var actualId = InvocationIdCallInvoker.GetCurrentInvocationId();

        // Assert
        Assert.Equal(expectedId, actualId);

        // Cleanup
        InvocationIdCallInvoker.SetCurrentInvocationId(null);
    }

    [Fact]
    public void GetCurrentInvocationId_ReturnsNullByDefault()
    {
        // Arrange - ensure clean state
        InvocationIdCallInvoker.SetCurrentInvocationId(null);

        // Act
        var actualId = InvocationIdCallInvoker.GetCurrentInvocationId();

        // Assert
        Assert.Null(actualId);
    }

    [Fact]
    public void SetCurrentInvocationId_CanBeCleared()
    {
        // Arrange
        InvocationIdCallInvoker.SetCurrentInvocationId("some-id");

        // Act
        InvocationIdCallInvoker.SetCurrentInvocationId(null);
        var actualId = InvocationIdCallInvoker.GetCurrentInvocationId();

        // Assert
        Assert.Null(actualId);
    }

    [Fact]
    public void SetCurrentInvocationId_CanBeOverwritten()
    {
        // Arrange
        const string firstId = "first-id";
        const string secondId = "second-id";

        // Act
        InvocationIdCallInvoker.SetCurrentInvocationId(firstId);
        InvocationIdCallInvoker.SetCurrentInvocationId(secondId);
        var actualId = InvocationIdCallInvoker.GetCurrentInvocationId();

        // Assert
        Assert.Equal(secondId, actualId);

        // Cleanup
        InvocationIdCallInvoker.SetCurrentInvocationId(null);
    }
}
