// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Moq;
using Xunit;

namespace Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Tests;

public class InvocationIdCallInvokerTests
{
    private const string InvocationIdMetadataKey = "x-azure-functions-invocationid";

    private static AsyncUnaryCall<string> CreateAsyncUnaryCallResponse(string response = "response")
    {
        return new AsyncUnaryCall<string>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

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
        // Arrange - nothing to do

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

    [Fact]
    public void BlockingUnaryCall_AddsInvocationIdToMetadata()
    {
        // Arrange
        const string expectedInvocationId = "test-invocation-id";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.BlockingUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns("response");

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);

        InvocationIdCallInvoker.SetCurrentInvocationId(expectedInvocationId);

        try
        {
            // Act
            invoker.BlockingUnaryCall(method, null, new CallOptions(), "request");

            // Assert
            Assert.NotNull(capturedOptions.Headers);
            var header = capturedOptions.Headers.FirstOrDefault(h => h.Key == InvocationIdMetadataKey);
            Assert.NotNull(header);
            Assert.Equal(expectedInvocationId, header.Value);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }

    [Fact]
    public void BlockingUnaryCall_PreservesExistingHeaders()
    {
        // Arrange
        const string expectedInvocationId = "test-invocation-id";
        const string existingHeaderKey = "existing-header";
        const string existingHeaderValue = "existing-value";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.BlockingUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns("response");

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var originalHeaders = new Metadata { { existingHeaderKey, existingHeaderValue } };

        InvocationIdCallInvoker.SetCurrentInvocationId(expectedInvocationId);

        try
        {
            // Act
            invoker.BlockingUnaryCall(method, null, new CallOptions(headers: originalHeaders), "request");

            // Assert
            Assert.NotNull(capturedOptions.Headers);

            // Verify invocation ID was added
            var invocationHeader = capturedOptions.Headers.FirstOrDefault(h => h.Key == InvocationIdMetadataKey);
            Assert.NotNull(invocationHeader);
            Assert.Equal(expectedInvocationId, invocationHeader.Value);

            // Verify existing header was preserved
            var existingHeader = capturedOptions.Headers.FirstOrDefault(h => h.Key == existingHeaderKey);
            Assert.NotNull(existingHeader);
            Assert.Equal(existingHeaderValue, existingHeader.Value);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }

    [Fact]
    public void BlockingUnaryCall_DoesNotMutateOriginalHeaders()
    {
        // Arrange
        const string expectedInvocationId = "test-invocation-id";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.BlockingUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns("response");

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var originalHeaders = new Metadata { { "original-key", "original-value" } };
        int originalCount = originalHeaders.Count;

        InvocationIdCallInvoker.SetCurrentInvocationId(expectedInvocationId);

        try
        {
            // Act
            invoker.BlockingUnaryCall(method, null, new CallOptions(headers: originalHeaders), "request");

            // Assert - original headers should not be mutated
            Assert.Equal(originalCount, originalHeaders.Count);
            Assert.DoesNotContain(originalHeaders, h => h.Key == InvocationIdMetadataKey);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }

    [Fact]
    public void BlockingUnaryCall_DoesNotAddHeaderWhenNoInvocationId()
    {
        // Arrange
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.BlockingUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns("response");

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);

        InvocationIdCallInvoker.SetCurrentInvocationId(null);

        // Act
        invoker.BlockingUnaryCall(method, null, new CallOptions(), "request");

        // Assert - no headers should be added when invocation ID is null
        Assert.Null(capturedOptions.Headers);
    }

    [Fact]
    public void BlockingUnaryCall_ReplacesExistingInvocationIdHeader()
    {
        // Arrange
        const string newInvocationId = "new-invocation-id";
        const string oldInvocationId = "old-invocation-id";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.BlockingUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns("response");

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var originalHeaders = new Metadata { { InvocationIdMetadataKey, oldInvocationId } };

        InvocationIdCallInvoker.SetCurrentInvocationId(newInvocationId);

        try
        {
            // Act
            invoker.BlockingUnaryCall(method, null, new CallOptions(headers: originalHeaders), "request");

            // Assert - should have exactly one invocation ID header with the new value
            Assert.NotNull(capturedOptions.Headers);
            var invocationHeaders = capturedOptions.Headers.Where(h => h.Key == InvocationIdMetadataKey).ToList();
            Assert.Single(invocationHeaders);
            Assert.Equal(newInvocationId, invocationHeaders[0].Value);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }

    [Fact]
    public void AsyncUnaryCall_AddsInvocationIdToMetadata()
    {
        // Arrange
        const string expectedInvocationId = "test-invocation-id-async";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.AsyncUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns(CreateAsyncUnaryCallResponse());

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);

        InvocationIdCallInvoker.SetCurrentInvocationId(expectedInvocationId);

        try
        {
            // Act
            using var call = invoker.AsyncUnaryCall(method, null, new CallOptions(), "request");

            // Assert
            Assert.NotNull(capturedOptions.Headers);
            var header = capturedOptions.Headers.FirstOrDefault(h => h.Key == InvocationIdMetadataKey);
            Assert.NotNull(header);
            Assert.Equal(expectedInvocationId, header.Value);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }

    [Fact]
    public void AsyncUnaryCall_PreservesExistingHeaders()
    {
        // Arrange
        const string expectedInvocationId = "test-invocation-id-async";
        const string existingHeaderKey = "existing-header";
        const string existingHeaderValue = "existing-value";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.AsyncUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns(CreateAsyncUnaryCallResponse());

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var originalHeaders = new Metadata { { existingHeaderKey, existingHeaderValue } };

        InvocationIdCallInvoker.SetCurrentInvocationId(expectedInvocationId);

        try
        {
            // Act
            using var call = invoker.AsyncUnaryCall(method, null, new CallOptions(headers: originalHeaders), "request");

            // Assert
            Assert.NotNull(capturedOptions.Headers);

            // Verify invocation ID was added
            var invocationHeader = capturedOptions.Headers.FirstOrDefault(h => h.Key == InvocationIdMetadataKey);
            Assert.NotNull(invocationHeader);
            Assert.Equal(expectedInvocationId, invocationHeader.Value);

            // Verify existing header was preserved
            var existingHeader = capturedOptions.Headers.FirstOrDefault(h => h.Key == existingHeaderKey);
            Assert.NotNull(existingHeader);
            Assert.Equal(existingHeaderValue, existingHeader.Value);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }

    [Fact]
    public void AsyncUnaryCall_DoesNotMutateOriginalHeaders()
    {
        // Arrange
        const string expectedInvocationId = "test-invocation-id-async";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.AsyncUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns(CreateAsyncUnaryCallResponse());

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var originalHeaders = new Metadata { { "original-key", "original-value" } };
        int originalCount = originalHeaders.Count;

        InvocationIdCallInvoker.SetCurrentInvocationId(expectedInvocationId);

        try
        {
            // Act
            using var call = invoker.AsyncUnaryCall(method, null, new CallOptions(headers: originalHeaders), "request");

            // Assert - original headers should not be mutated
            Assert.Equal(originalCount, originalHeaders.Count);
            Assert.DoesNotContain(originalHeaders, h => h.Key == InvocationIdMetadataKey);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }

    [Fact]
    public void AsyncUnaryCall_DoesNotAddHeaderWhenNoInvocationId()
    {
        // Arrange
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.AsyncUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns(CreateAsyncUnaryCallResponse());

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);

        InvocationIdCallInvoker.SetCurrentInvocationId(null);

        // Act
        using var call = invoker.AsyncUnaryCall(method, null, new CallOptions(), "request");

        // Assert - no headers should be added when invocation ID is null
        Assert.Null(capturedOptions.Headers);
    }

    [Fact]
    public void AsyncUnaryCall_ReplacesExistingInvocationIdHeader()
    {
        // Arrange
        const string newInvocationId = "new-invocation-id";
        const string oldInvocationId = "old-invocation-id";
        CallOptions capturedOptions = default;

        var mockInner = new Mock<CallInvoker>();
        mockInner
            .Setup(x => x.AsyncUnaryCall(
                It.IsAny<Method<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CallOptions>(),
                It.IsAny<string>()))
            .Callback<Method<string, string>, string, CallOptions, string>((m, h, o, r) => capturedOptions = o)
            .Returns(CreateAsyncUnaryCallResponse());

        var invoker = new InvocationIdCallInvoker(mockInner.Object);
        var method = new Method<string, string>(MethodType.Unary, "TestService", "TestMethod", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        var originalHeaders = new Metadata { { InvocationIdMetadataKey, oldInvocationId } };

        InvocationIdCallInvoker.SetCurrentInvocationId(newInvocationId);

        try
        {
            // Act
            using var call = invoker.AsyncUnaryCall(method, null, new CallOptions(headers: originalHeaders), "request");

            // Assert - should have exactly one invocation ID header with the new value
            Assert.NotNull(capturedOptions.Headers);
            var invocationHeaders = capturedOptions.Headers.Where(h => h.Key == InvocationIdMetadataKey).ToList();
            Assert.Single(invocationHeaders);
            Assert.Equal(newInvocationId, invocationHeaders[0].Value);
        }
        finally
        {
            InvocationIdCallInvoker.SetCurrentInvocationId(null);
        }
    }
}
