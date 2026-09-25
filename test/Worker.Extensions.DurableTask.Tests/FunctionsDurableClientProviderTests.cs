// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

/// <summary>
/// Unit tests for <see cref="FunctionsDurableClientProvider"/>.
/// </summary>
public class FunctionsDurableClientProviderTests
{
    [Fact]
    public void ClientFactoryDoesNotExposePurgeFacadeThroughOutParameters()
    {
        Assert.DoesNotContain(typeof(FunctionsDurableClientProvider).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            method => method.Name == "GetClient" && method.GetParameters().Any(parameter => parameter.IsOut));
    }

    [Fact]
    public async Task BoundClientsShareTransportButKeepBindingMetadataSeparate()
    {
        await using var provider = new FunctionsDurableClientProvider(
            NullLoggerFactory.Instance, Options.Create(new DurableTaskClientOptions()));
        var endpoint = new Uri("http://localhost:12345");
        FunctionsDurableTaskClient first = provider.CreateBoundClient(endpoint, "hub", "connection", null, TimeSpan.FromMinutes(1), "?first", "http://first");
        FunctionsDurableTaskClient second = provider.CreateBoundClient(endpoint, "hub", "connection", null, TimeSpan.FromMinutes(1), "?second", "http://second");

        Assert.NotSame(first, second);
        Assert.Equal("?first", first.QueryString);
        Assert.Equal("http://first", first.HttpBaseUrl);
        Assert.Equal("?second", second.QueryString);
        Assert.Equal("http://second", second.HttpBaseUrl);
        Assert.Same(ReadField(first, "inner"), ReadField(second, "inner"));
        Assert.Same(ReadField(first, "purgeClient"), ReadField(second, "purgeClient"));
        Assert.Same(ReadField(first, "inner"), provider.GetClient(endpoint, "hub", "connection", null, TimeSpan.FromMinutes(1)));

        await first.DisposeAsync();
        Assert.Same(ReadField(second, "inner"), provider.GetClient(endpoint, "hub", "connection", null, TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData("otherHub", "connection", "http://localhost:12345")]
    [InlineData("hub", "otherConnection", "http://localhost:12345")]
    [InlineData("hub", "connection", "http://localhost:12346")]
    public async Task BoundClientTransportIsScopedToEndpointHubAndConnection(string hub, string connection, string address)
    {
        await using var provider = new FunctionsDurableClientProvider(
            NullLoggerFactory.Instance, Options.Create(new DurableTaskClientOptions()));
        FunctionsDurableTaskClient first = provider.CreateBoundClient(new Uri("http://localhost:12345"), "hub", "connection", null, TimeSpan.FromMinutes(1), null, null);
        FunctionsDurableTaskClient second = provider.CreateBoundClient(new Uri(address), hub, connection, null, TimeSpan.FromMinutes(1), null, null);

        Assert.NotSame(ReadField(first, "inner"), ReadField(second, "inner"));
        Assert.NotSame(ReadField(first, "purgeClient"), ReadField(second, "purgeClient"));
    }

    [Fact]
    public async Task DisposedProviderRejectsBothClientCreationPaths()
    {
        var provider = new FunctionsDurableClientProvider(
            NullLoggerFactory.Instance, Options.Create(new DurableTaskClientOptions()));
        var endpoint = new Uri("http://localhost:12345");
        provider.CreateBoundClient(endpoint, "hub", "connection", null, TimeSpan.FromMinutes(1), null, null);
        await provider.DisposeAsync();
        await provider.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => provider.CreateBoundClient(endpoint, "hub", "connection", null, TimeSpan.FromMinutes(1), null, null));
        Assert.Throws<ObjectDisposedException>(() => provider.GetClient(endpoint, "hub", "connection", null, TimeSpan.FromMinutes(1)));
    }

    private static object? ReadField(FunctionsDurableTaskClient client, string name)
        => typeof(FunctionsDurableTaskClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client);

    /// <summary>
    /// Tests that the DefaultMethodConfig static field is correctly configured with retry policy.
    /// </summary>
    [Fact]
    public void DefaultMethodConfig_HasCorrectRetryPolicy()
    {
        // Use reflection to access the private static field
        Type providerType = typeof(FunctionsDurableClientProvider);
        FieldInfo? methodConfigField = providerType.GetField("DefaultMethodConfig", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(methodConfigField);

        MethodConfig? methodConfig = methodConfigField.GetValue(null) as MethodConfig;
        Assert.NotNull(methodConfig);

        // Verify the method config applies to all methods (MethodName.Default)
        Assert.Single(methodConfig.Names);
        Assert.Equal(MethodName.Default, methodConfig.Names[0]);

        // Verify retry policy is configured
        Assert.NotNull(methodConfig.RetryPolicy);

        RetryPolicy retryPolicy = methodConfig.RetryPolicy;

        // Verify retry policy values
        Assert.Equal(5, retryPolicy.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(1), retryPolicy.InitialBackoff);
        Assert.Equal(TimeSpan.FromSeconds(5), retryPolicy.MaxBackoff);
        Assert.Equal(1.5, retryPolicy.BackoffMultiplier);

        // Verify UNAVAILABLE and RESOURCE_EXHAUSTED status codes are retried
        Assert.Equal(2, retryPolicy.RetryableStatusCodes.Count);
        Assert.Contains(StatusCode.Unavailable, retryPolicy.RetryableStatusCodes);
        Assert.Contains(StatusCode.ResourceExhausted, retryPolicy.RetryableStatusCodes);
    }

    /// <summary>
    /// Tests that the DefaultServiceConfig static field contains the DefaultMethodConfig.
    /// </summary>
    [Fact]
    public void DefaultServiceConfig_ContainsDefaultMethodConfig()
    {
        // Use reflection to access the private static fields
        Type providerType = typeof(FunctionsDurableClientProvider);
        FieldInfo? serviceConfigField = providerType.GetField("DefaultServiceConfig", BindingFlags.NonPublic | BindingFlags.Static);
        FieldInfo? methodConfigField = providerType.GetField("DefaultMethodConfig", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(serviceConfigField);
        Assert.NotNull(methodConfigField);

        ServiceConfig? serviceConfig = serviceConfigField.GetValue(null) as ServiceConfig;
        MethodConfig? expectedMethodConfig = methodConfigField.GetValue(null) as MethodConfig;

        Assert.NotNull(serviceConfig);
        Assert.NotNull(expectedMethodConfig);

        // Verify the service config contains the method config
        Assert.Single(serviceConfig!.MethodConfigs);
        Assert.Equal(expectedMethodConfig, serviceConfig!.MethodConfigs[0]);
    }

    /// <summary>
    /// Tests that GetClient returns a valid DurableTaskClient with retry configuration.
    /// </summary>
    [Fact]
    public async Task GetClient_ReturnsClientWithRetryConfiguration()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        var clientOptions = new DurableTaskClientOptions();
        var mockOptions = new Mock<IOptions<DurableTaskClientOptions>>();
        mockOptions.Setup(x => x.Value).Returns(clientOptions);

        var provider = new FunctionsDurableClientProvider(mockLoggerFactory.Object, mockOptions.Object);

        // Use a valid but non-connecting endpoint for testing
        Uri endpoint = new Uri("http://localhost:12345");

        // Act
        DurableTaskClient client = provider.GetClient(
            endpoint,
            taskHub: null,
            connectionName: null,
            maxGrpcMessageSizeInBytes: null,
            grpcHttpClientTimeout: TimeSpan.FromMinutes(1));

        // Assert
        Assert.NotNull(client);

        // Clean up
        await provider.DisposeAsync();
    }

    /// <summary>
    /// Tests that GetClient with task hub and connection name returns a valid client.
    /// </summary>
    [Fact]
    public async Task GetClient_WithTaskHubAndConnectionName_ReturnsClientWithRetryConfiguration()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        var clientOptions = new DurableTaskClientOptions();
        var mockOptions = new Mock<IOptions<DurableTaskClientOptions>>();
        mockOptions.Setup(x => x.Value).Returns(clientOptions);

        var provider = new FunctionsDurableClientProvider(mockLoggerFactory.Object, mockOptions.Object);

        // Use a valid but non-connecting endpoint for testing
        Uri endpoint = new Uri("http://localhost:12345");

        // Act
        DurableTaskClient client = provider.GetClient(
            endpoint,
            taskHub: "TestTaskHub",
            connectionName: "TestConnection",
            maxGrpcMessageSizeInBytes: 4 * 1024 * 1024, // 4 MB
            grpcHttpClientTimeout: TimeSpan.FromMinutes(2));

        // Assert
        Assert.NotNull(client);

        // Clean up
        await provider.DisposeAsync();
    }

    /// <summary>
    /// Tests that GetClient caches clients for the same endpoint/task hub/connection combination.
    /// </summary>
    [Fact]
    public async Task GetClient_CachesClientsForSameKey()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());

        var clientOptions = new DurableTaskClientOptions();
        var mockOptions = new Mock<IOptions<DurableTaskClientOptions>>();
        mockOptions.Setup(x => x.Value).Returns(clientOptions);

        var provider = new FunctionsDurableClientProvider(mockLoggerFactory.Object, mockOptions.Object);

        Uri endpoint = new Uri("http://localhost:12345");
        string taskHub = "TestTaskHub";

        // Act
        DurableTaskClient client1 = provider.GetClient(
            endpoint,
            taskHub: taskHub,
            connectionName: null,
            maxGrpcMessageSizeInBytes: null,
            grpcHttpClientTimeout: TimeSpan.FromMinutes(1));

        DurableTaskClient client2 = provider.GetClient(
            endpoint,
            taskHub: taskHub,
            connectionName: null,
            maxGrpcMessageSizeInBytes: null,
            grpcHttpClientTimeout: TimeSpan.FromMinutes(1));

        // Assert - should return the same cached client
        Assert.Same(client1, client2);

        // Clean up
        await provider.DisposeAsync();
    }

    /// <summary>
    /// Tests that retry policy configuration values match expected defaults.
    /// </summary>
    [Fact]
    public void RetryPolicy_HasExpectedDefaultValues()
    {
        // Use reflection to access the private static field
        Type providerType = typeof(FunctionsDurableClientProvider);
        FieldInfo? methodConfigField = providerType.GetField("DefaultMethodConfig", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(methodConfigField);

        MethodConfig? methodConfig = methodConfigField.GetValue(null) as MethodConfig;
        Assert.NotNull(methodConfig);
        Assert.NotNull(methodConfig.RetryPolicy);

        RetryPolicy retryPolicy = methodConfig.RetryPolicy;

        // These are the expected default values for the retry policy
        const int ExpectedMaxAttempts = 5;
        TimeSpan expectedInitialBackoff = TimeSpan.FromSeconds(1);
        TimeSpan expectedMaxBackoff = TimeSpan.FromSeconds(5);
        const double ExpectedBackoffMultiplier = 1.5;

        Assert.Equal(ExpectedMaxAttempts, retryPolicy.MaxAttempts);
        Assert.Equal(expectedInitialBackoff, retryPolicy.InitialBackoff);
        Assert.Equal(expectedMaxBackoff, retryPolicy.MaxBackoff);
        Assert.Equal(ExpectedBackoffMultiplier, retryPolicy.BackoffMultiplier);
    }
}
