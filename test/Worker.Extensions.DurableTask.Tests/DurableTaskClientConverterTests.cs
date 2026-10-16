// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.LargePayloadPurge.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class DurableTaskClientConverterTests
{
    [Fact]
    public async Task ActualBindingJsonCreatesOrdinaryAndPurgeFacadeWithPerBindingManagementUrls()
    {
        await using var provider = CreateProvider();
        var converter = new DurableTaskClientConverter(provider);
        var first = await Convert(converter, Binding("hub-a", "connection-a", "code=first", "https://first.example/runtime/webhooks/durabletask"));
        var second = await Convert(converter, Binding("hub-a", "connection-a", "code=second", "https://second.example/runtime/webhooks/durabletask"));

        Assert.NotSame(first, second);
        Assert.IsAssignableFrom<DurableTaskClient>(first);
        Assert.IsAssignableFrom<ILargePayloadPurgeClient>(first);
        Assert.Same(Field(first, "inner"), Field(second, "inner"));
        Assert.Same(Field(first, "purgeClient"), Field(second, "purgeClient"));
        Assert.Equal("code=first", first.QueryString);
        Assert.Equal("code=second", second.QueryString);
        Assert.Equal("https://first.example/runtime/webhooks/durabletask", first.HttpBaseUrl);
        Assert.Equal("https://second.example/runtime/webhooks/durabletask", second.HttpBaseUrl);
        Assert.Contains("https://first.example/runtime/webhooks/durabletask", first.CreateHttpManagementPayload("instance").StatusQueryGetUri);
        Assert.Contains("code=first", first.CreateHttpManagementPayload("instance").StatusQueryGetUri);
        Assert.Contains("code=second", second.CreateHttpManagementPayload("instance").StatusQueryGetUri);
        await first.DisposeAsync();
        Assert.Same(Field(second, "inner"), provider.GetClient(new Uri("http://127.0.0.1:12345"), "hub-a", "connection-a", 4194304, TimeSpan.FromSeconds(90)));
    }

    [Theory]
    [InlineData("hub-b", "connection-a")]
    [InlineData("hub-a", "connection-b")]
    public async Task ActualBindingJsonKeepsTransportScopedToHubAndConnection(string hub, string connection)
    {
        await using var provider = CreateProvider();
        var converter = new DurableTaskClientConverter(provider);
        var first = await Convert(converter, Binding("hub-a", "connection-a", null, null));
        var second = await Convert(converter, Binding(hub, connection, null, null));
        Assert.NotSame(Field(first, "inner"), Field(second, "inner"));
        Assert.NotSame(Field(first, "purgeClient"), Field(second, "purgeClient"));
    }

    [Fact]
    public async Task MinimalValidPayloadRetainsLegacyDefaults()
    {
        await using var provider = CreateProvider();
        var converter = new DurableTaskClientConverter(provider);
        var client = await Convert(converter, """{"rpcBaseUrl":"http://127.0.0.1:12345"}""");
        Assert.Null(client.QueryString);
        Assert.Null(client.HttpBaseUrl);
        Assert.Same(Field(client, "inner"), provider.GetClient(new Uri("http://127.0.0.1:12345"), null, null, 0, TimeSpan.FromSeconds(100)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(123)]
    public async Task NonStringBindingPayloadFailsExplicitly(object? source)
    {
        await using var provider = CreateProvider();
        ConversionResult result = await new DurableTaskClientConverter(provider).ConvertAsync(Context(source));
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.IsType<InvalidOperationException>(result.Error);
        Assert.Contains("string payload", result.Error.Message);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"rpcBaseUrl":"not-a-uri"}""")]
    [InlineData("""{"rpcBaseUrl":"http://127.0.0.1:12345","grpcHttpClientTimeout":"invalid"}""")]
    public async Task InvalidBindingPayloadFailsWithoutReturningClient(string source)
    {
        await using var provider = CreateProvider();
        ConversionResult result = await new DurableTaskClientConverter(provider).ConvertAsync(Context(source));
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.IsType<InvalidOperationException>(result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task DifferentBindingTargetRemainsUnhandled()
    {
        await using var provider = CreateProvider();
        var context = new Mock<ConverterContext>();
        context.SetupGet(value => value.TargetType).Returns(typeof(string));
        context.SetupGet(value => value.Source).Returns("irrelevant");
        Assert.Equal(ConversionStatus.Unhandled, (await new DurableTaskClientConverter(provider).ConvertAsync(context.Object)).Status);
    }

    [Fact]
    public async Task DisposedProviderReturnsConversionFailureWithOriginalCause()
    {
        var provider = CreateProvider();
        var converter = new DurableTaskClientConverter(provider);
        await provider.DisposeAsync();
        ConversionResult result = await converter.ConvertAsync(Context(Binding("hub", "connection", null, null)));
        Assert.Equal(ConversionStatus.Failed, result.Status);
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(result.Error);
        Assert.IsType<ObjectDisposedException>(error.InnerException);
    }

    private static FunctionsDurableClientProvider CreateProvider() =>
        new(NullLoggerFactory.Instance, Options.Create(new DurableTaskClientOptions()));

    private static string Binding(string hub, string connection, string? query, string? baseUrl) =>
        JsonSerializer.Serialize(new
        {
            rpcBaseUrl = "http://127.0.0.1:12345",
            taskHubName = hub,
            connectionName = connection,
            requiredQueryStringParameters = query,
            httpBaseUrl = baseUrl,
            maxGrpcMessageSizeInBytes = 4194304,
            grpcHttpClientTimeout = JsonSerializer.Serialize(TimeSpan.FromSeconds(90)),
        });

    private static ConverterContext Context(object? source)
    {
        var context = new Mock<ConverterContext>();
        context.SetupGet(value => value.TargetType).Returns(typeof(DurableTaskClient));
        context.SetupGet(value => value.Source).Returns(source);
        return context.Object;
    }

    private static async Task<FunctionsDurableTaskClient> Convert(DurableTaskClientConverter converter, string source)
    {
        ConversionResult result = await converter.ConvertAsync(Context(source));
        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        return Assert.IsType<FunctionsDurableTaskClient>(result.Value);
    }

    private static object Field(FunctionsDurableTaskClient client, string name)
    {
        FieldInfo? field = typeof(FunctionsDurableTaskClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        object? value = field.GetValue(client);
        Assert.NotNull(value);
        return value;
    }
}
