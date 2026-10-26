// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Buffers;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ExternalizedClientTests
{
    [Fact]
    public async Task Client_StartAndRead_ExternalizesRequest_AndHydratesResponse()
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        var transport = new MemoryCallInvoker();
        var client = new P.TaskHubSidecarService.TaskHubSidecarServiceClient(
            transport.Intercept(payloads.ClientInterceptor));
        string input = System.Text.Json.JsonSerializer.Serialize(new string('x', 1_100_000));

        await client.StartInstanceAsync(new P.CreateInstanceRequest { Name = "Orchestration", Input = input });
        P.GetInstanceResponse response = await client.GetInstanceAsync(new P.GetInstanceRequest { InstanceId = "instance" });

        Assert.NotNull(transport.Request);
        Assert.StartsWith("\"blob:v1:", transport.Request.Input);
        Assert.True(transport.Request.CalculateSize() < 1024);
        Assert.Equal(input, response.OrchestrationState.Input);
        Assert.Equal(input, response.OrchestrationState.Output);
        Assert.Equal(1, store.Uploads);
        Assert.Equal(1, store.Downloads);
    }

    [Fact]
    public async Task Client_Event_ExternalizesEventData()
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        var transport = new MemoryCallInvoker();
        var client = new P.TaskHubSidecarService.TaskHubSidecarServiceClient(
            transport.Intercept(payloads.ClientInterceptor));

        await client.RaiseEventAsync(new P.RaiseEventRequest
        {
            InstanceId = "instance",
            Name = "event",
            Input = new string('x', 1_100_000),
        });

        Assert.NotNull(transport.Event);
        Assert.StartsWith("\"blob:v1:", transport.Event.Input);
        Assert.True(transport.Event.CalculateSize() < 1024);
    }

    [Fact]
    public async Task Client_StreamingHistory_HydratesExplicitlyRequestedPayloads()
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string data = System.Text.Json.JsonSerializer.Serialize(new string('x', 1_100_000));
        string reference = (await payloads.ExternalizeAsync(data, CancellationToken.None))!;
        var transport = new MemoryCallInvoker
        {
            History = new P.HistoryChunk
            {
                Events =
                {
                    new P.HistoryEvent { TaskScheduled = new P.TaskScheduledEvent { Input = $"[{reference}]" } },
                    new P.HistoryEvent { TaskCompleted = new P.TaskCompletedEvent { Result = reference } },
                },
            },
        };
        var client = new P.TaskHubSidecarService.TaskHubSidecarServiceClient(
            transport.Intercept(payloads.ClientInterceptor));

        using AsyncServerStreamingCall<P.HistoryChunk> call = client.StreamInstanceHistory(
            new P.StreamInstanceHistoryRequest { InstanceId = "instance" });
        Assert.True(await call.ResponseStream.MoveNext(CancellationToken.None));

        Assert.Equal($"[{data}]", call.ResponseStream.Current.Events[0].TaskScheduled.Input);
        Assert.Equal(data, call.ResponseStream.Current.Events[1].TaskCompleted.Result);
        Assert.Equal(reference, transport.History.Events[1].TaskCompleted.Result);
        Assert.Equal(1, store.Downloads);
        Assert.False(await call.ResponseStream.MoveNext(CancellationToken.None));
    }

    [Fact]
    public async Task BoundClientProvider_AttachesPayloadInterceptor_AndPreservesCache()
    {
        var payloads = new RecordingPayloads();
        await using var provider = new FunctionsDurableClientProvider(
            NullLoggerFactory.Instance, Options.Create(new DurableTaskClientOptions()), payloads);
        var endpoint = new Uri("http://localhost:12345");

        DurableTaskClient first = provider.GetClient(endpoint, "hub", "connection", null, TimeSpan.FromSeconds(30));
        DurableTaskClient second = provider.GetClient(endpoint, "hub", "connection", null, TimeSpan.FromSeconds(30));

        Assert.Same(first, second);
        Assert.Equal(1, payloads.InterceptorReads);
    }

    private sealed class MemoryCallInvoker : CallInvoker
    {
        public P.CreateInstanceRequest? Request { get; private set; }

        public P.RaiseEventRequest? Event { get; private set; }

        public P.HistoryChunk? History { get; init; }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            IMessage response;
            switch (request)
            {
                case P.CreateInstanceRequest start:
                    this.Request = start.Clone();
                    response = new P.CreateInstanceResponse { InstanceId = "instance" };
                    break;
                case P.GetInstanceRequest:
                    response = new P.GetInstanceResponse
                    {
                        Exists = true,
                        OrchestrationState = new P.OrchestrationState
                        {
                            InstanceId = "instance",
                            Input = this.Request!.Input,
                            Output = this.Request.Input,
                        },
                    };
                    break;
                case P.RaiseEventRequest raised:
                    this.Event = raised.Clone();
                    response = new P.RaiseEventResponse();
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.");
            }

            TResponse result = method.ResponseMarshaller.ContextualDeserializer(
                new TestDeserializationContext(response.ToByteArray()));
            return new AsyncUnaryCall<TResponse>(
                Task.FromResult(result), Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            => throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            Assert.IsType<P.StreamInstanceHistoryRequest>(request);
            TResponse response = method.ResponseMarshaller.ContextualDeserializer(
                new TestDeserializationContext(this.History!.ToByteArray()));
            return new AsyncServerStreamingCall<TResponse>(
                new SingleResponseReader<TResponse>(response),
                Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options)
            => throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options)
            => throw new NotSupportedException();
    }

    private sealed class SingleResponseReader<T>(T response) : IAsyncStreamReader<T>
    {
        private bool read;

        public T Current => response;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool hasNext = !this.read;
            this.read = true;
            return Task.FromResult(hasNext);
        }
    }

    private sealed class RecordingPayloads : IExternalizedPayloads
    {
        public int InterceptorReads { get; private set; }

        public Interceptor ClientInterceptor
        {
            get
            {
                this.InterceptorReads++;
                return new PassThroughInterceptor();
            }
        }

        public void ValidateSize(string? payload)
        {
        }

        public Task<string?> ExternalizeAsync(string? payload, CancellationToken cancellationToken)
            => Task.FromResult(payload);

        public Task<string?> ResolveAsync(string? payload, CancellationToken cancellationToken)
            => Task.FromResult(payload);
    }

    private sealed class PassThroughInterceptor : Interceptor
    {
    }

    private sealed class TestDeserializationContext(byte[] bytes) : DeserializationContext
    {
        public override int PayloadLength => bytes.Length;

        public override byte[] PayloadAsNewBuffer() => bytes;

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence() => new(bytes);
    }
}
