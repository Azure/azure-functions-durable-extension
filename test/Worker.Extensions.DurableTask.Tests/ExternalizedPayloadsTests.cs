// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker.Grpc;
using Microsoft.Extensions.DependencyInjection;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ExternalizedPayloadsTests
{
    [Fact]
    public async Task LargePayload_UsesJsonReference_AndRoundTrips()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string original = JsonSerializer.Serialize(new string('x', 1_100_000));

        string? reference = await payloads.ExternalizeAsync(original, CancellationToken.None);

        Assert.NotNull(reference);
        Assert.StartsWith("\"blob:v1:", reference);
        Assert.True(store.IsKnownPayloadToken(JsonSerializer.Deserialize<string>(reference)!));
        Assert.True(Encoding.UTF8.GetByteCount(reference) < 256);
        Assert.True(Encoding.UTF8.GetByteCount(original) > 1024 * 1024);
        Assert.Equal(original, await payloads.ResolveAsync(reference, CancellationToken.None));
        Assert.Equal(1, store.Uploads);
    }

    [Fact]
    public async Task OrchestrationHydration_DoesNotMutateHostHistory_OrDownloadScheduledInputs()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string original = JsonSerializer.Serialize(new string('x', 1_100_000));
        string? reference = await payloads.ExternalizeAsync(original, CancellationToken.None);
        var hostRequest = new P.OrchestratorRequest
        {
            InstanceId = "orchestration",
            PastEvents =
            {
                new P.HistoryEvent { ExecutionStarted = new P.ExecutionStartedEvent { Input = reference } },
                new P.HistoryEvent
                {
                    TaskScheduled = new P.TaskScheduledEvent
                    {
                        Name = "Activity",
                        Input = "\"blob:v1:payloads:missing-historical-input\"",
                    },
                },
            },
            NewEvents =
            {
                new P.HistoryEvent { TaskCompleted = new P.TaskCompletedEvent { Result = reference } },
            },
        };
        byte[] hostBytes = hostRequest.ToByteArray();

        string hydrated = await ExternalizedPayloads.TransformInvocationAsync(
            Convert.ToBase64String(hostBytes), P.OrchestratorRequest.Parser, payloads,
            externalize: false, CancellationToken.None);
        P.OrchestratorRequest workerRequest = P.OrchestratorRequest.Parser.ParseFrom(Convert.FromBase64String(hydrated));

        Assert.Equal(original, workerRequest.PastEvents[0].ExecutionStarted.Input);
        Assert.Equal(original, workerRequest.NewEvents[0].TaskCompleted.Result);
        Assert.Equal(reference, hostRequest.NewEvents[0].TaskCompleted.Result);
        Assert.Equal(hostBytes, hostRequest.ToByteArray());
        Assert.Equal(1, store.Downloads);
    }

    [Fact]
    public async Task OrchestrationResponse_ExternalizesPayloads_NotMetadata_AndReusesReferences()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store, threshold: 256);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string data = JsonSerializer.Serialize(new string('x', 2048));
        string metadata = new('v', 2048);
        var response = new P.OrchestratorResponse
        {
            InstanceId = "orchestration",
            CustomStatus = data,
            Actions =
            {
                new P.OrchestratorAction
                {
                    ScheduleTask = new P.ScheduleTaskAction { Name = "Activity", Version = metadata, Input = $"[{data}]" },
                },
                new P.OrchestratorAction
                {
                    CompleteOrchestration = new P.CompleteOrchestrationAction { Result = data },
                },
            },
        };

        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);

        Assert.StartsWith("\"blob:v1:", response.CustomStatus);
        Assert.Equal($"[{response.CustomStatus}]", response.Actions[0].ScheduleTask.Input);
        Assert.Equal(response.CustomStatus, response.Actions[1].CompleteOrchestration.Result);
        Assert.Equal(metadata, response.Actions[0].ScheduleTask.Version);
        Assert.Equal(1, store.Uploads);
        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);
        Assert.Equal(1, store.Uploads);
    }

    [Fact]
    public async Task OrchestrationRunner_ReceivesHydratedInput_ReturnsReferenceToHost()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string data = new('x', 1_100_000);
        string serialized = JsonSerializer.Serialize(data);
        string? reference = await payloads.ExternalizeAsync(serialized, CancellationToken.None);
        var request = new P.OrchestratorRequest
        {
            InstanceId = "instance",
            ExecutionId = "execution",
            NewEvents =
            {
                new P.HistoryEvent
                {
                    EventId = -1,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    OrchestratorStarted = new P.OrchestratorStartedEvent(),
                },
                new P.HistoryEvent
                {
                    EventId = 0,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    ExecutionStarted = new P.ExecutionStartedEvent
                    {
                        Name = "PayloadOrchestration",
                        Input = reference,
                        OrchestrationInstance = new P.OrchestrationInstance
                        {
                            InstanceId = "instance",
                            ExecutionId = "execution",
                        },
                    },
                },
            },
        };
        byte[] hostHistory = request.ToByteArray();
        string workerInput = await ExternalizedPayloads.TransformInvocationAsync(
            Convert.ToBase64String(hostHistory), P.OrchestratorRequest.Parser,
            payloads, externalize: false, CancellationToken.None);

        string output = GrpcOrchestrationRunner.LoadAndRun<string, string>(
            workerInput,
            (context, input) =>
            {
                Assert.Equal(data, input);
                context.SetCustomStatus(input);
                return Task.FromResult(input);
            });
        string hostOutput = await ExternalizedPayloads.TransformInvocationAsync(
            output, P.OrchestratorResponse.Parser, payloads, externalize: true, CancellationToken.None);
        P.OrchestratorResponse response = P.OrchestratorResponse.Parser.ParseFrom(Convert.FromBase64String(hostOutput));

        P.CompleteOrchestrationAction completed = Assert.Single(response.Actions).CompleteOrchestration;
        Assert.Equal(P.OrchestrationStatus.Completed, completed.OrchestrationStatus);
        Assert.StartsWith("\"blob:v1:", completed.Result);
        Assert.Equal(completed.Result, response.CustomStatus);
        Assert.Equal(serialized, await payloads.ResolveAsync(completed.Result, CancellationToken.None));
        Assert.True(hostOutput.Length < 2048);
        Assert.Equal(hostHistory, request.ToByteArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownToken_IsNotUploadedAgain(bool quoted)
    {
        var store = new MemoryPayloadStore();
        string raw = await store.UploadAsync("\"original\"", CancellationToken.None);
        using ServiceProvider services = CreateServices(store, threshold: 1);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();

        string? forwarded = await payloads.ExternalizeAsync(
            quoted ? JsonSerializer.Serialize(raw) : raw, CancellationToken.None);

        Assert.Equal(JsonSerializer.Serialize(raw), forwarded);
        Assert.Equal(1, store.Uploads);
        Assert.Equal("\"original\"", await payloads.ResolveAsync(forwarded, CancellationToken.None));
    }

    [Theory]
    [InlineData(255, false)]
    [InlineData(256, true)]
    public async Task Threshold_IsInclusive(int bytes, bool externalized)
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store, threshold: 256);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string original = new('x', bytes);

        string? result = await payloads.ExternalizeAsync(original, CancellationToken.None);

        Assert.Equal(externalized ? 1 : 0, store.Uploads);
        Assert.Equal(original, await payloads.ResolveAsync(result, CancellationToken.None));
    }

    [Fact]
    public async Task Threshold_UsesUtf8Bytes_NotCharacterCount()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store, threshold: 256);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string original = new('\u00e9', 128);

        string? result = await payloads.ExternalizeAsync(original, CancellationToken.None);

        Assert.Equal(1, store.Uploads);
        Assert.Equal(original, await payloads.ResolveAsync(result, CancellationToken.None));
    }

    [Fact]
    public async Task OversizedResponse_FailsBeforeAnyUploadOrMutation()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store, threshold: 256, maximum: 512);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        var response = new P.OrchestratorResponse
        {
            CustomStatus = new string('x', 300),
            Actions =
            {
                new P.OrchestratorAction
                {
                    ScheduleTask = new P.ScheduleTaskAction { Input = $"[{JsonSerializer.Serialize(new string('x', 600))}]" },
                },
            },
        };
        byte[] original = response.ToByteArray();

        await Assert.ThrowsAsync<PayloadStorageException>(() =>
            ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None));

        Assert.Equal(0, store.Uploads);
        Assert.Equal(original, response.ToByteArray());
    }

    [Fact]
    public async Task UploadFailure_IsNotReplacedWithAnInlineSuccess()
    {
        var failure = new IOException("Storage unavailable.");
        var store = new MemoryPayloadStore { UploadFailure = failure };
        using ServiceProvider services = CreateServices(store, threshold: 256);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();

        Assert.Same(failure, await Record.ExceptionAsync(() =>
            payloads.ExternalizeAsync(new string('x', 1024), CancellationToken.None)));
    }

    [Fact]
    public async Task MissingReference_IsNotReturnedAsLiteralUserData()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            payloads.ResolveAsync("\"blob:v1:payloads:missing\"", CancellationToken.None));
    }

    [Fact]
    public async Task LiteralReferenceInApplicationObject_IsNotHydrated()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        const string original = "{\"reference\":\"blob:v1:payloads:missing\"}";

        Assert.Equal(original, await payloads.ResolveAsync(original, CancellationToken.None));
        Assert.Equal(0, store.Downloads);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"value\":1}")]
    public async Task InlineAndNullPayloads_AreUnchanged(string? value)
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();

        Assert.Equal(value, await payloads.ExternalizeAsync(value, CancellationToken.None));
        Assert.Equal(value, await payloads.ResolveAsync(value, CancellationToken.None));
        Assert.Equal(0, store.Uploads);
        Assert.Equal(0, store.Downloads);
    }

    [Fact]
    public async Task EntityTransport_RemainsUnchanged()
    {
        var store = new MemoryPayloadStore();
        using ServiceProvider services = CreateServices(store, threshold: 256);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        var request = new P.SignalEntityRequest { Input = new string('x', 1024) };
        byte[] original = request.ToByteArray();

        await ExternalizedPayloads.TransformAsync(request, payloads, externalize: true, CancellationToken.None);

        Assert.Equal(original, request.ToByteArray());
        Assert.Equal(0, store.Uploads);
    }

    [Fact]
    public async Task Cancellation_AbortsPendingUpload()
    {
        var store = new MemoryPayloadStore { UploadGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using ServiceProvider services = CreateServices(store, threshold: 256);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        using var cancellation = new CancellationTokenSource();
        Task<string?> upload = payloads.ExternalizeAsync(new string('x', 1024), cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);
        Assert.Equal(0, store.Uploads);
    }

    [Fact]
    public void Registration_RequiresAnExplicitStore()
    {
        var services = new ServiceCollection();
        services.AddDurableTaskExternalizedPayloads();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IExternalizedPayloads>());
    }

    internal static ServiceProvider CreateServices(MemoryPayloadStore store, int threshold = 256 * 1024, int maximum = 10 * 1024 * 1024)
    {
        var services = new ServiceCollection();
        services.AddSingleton<PayloadStore>(store);
        services.Configure<LargePayloadStorageOptions>(options =>
        {
            options.ThresholdBytes = threshold;
            options.MaxPayloadBytes = maximum;
        });
        services.AddDurableTaskExternalizedPayloads();
        return services.BuildServiceProvider();
    }

    internal sealed class MemoryPayloadStore : PayloadStore
    {
        private readonly ConcurrentDictionary<string, string> payloads = new();
        private int uploads;
        private int downloads;

        public int Uploads => this.uploads;

        public int Downloads => this.downloads;

        public Exception? UploadFailure { get; init; }

        public TaskCompletionSource<bool>? UploadGate { get; init; }

        public override async Task<string> UploadAsync(string payLoad, CancellationToken cancellationToken)
        {
            if (this.UploadGate is not null)
            {
                await this.UploadGate.Task.WaitAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (this.UploadFailure is not null)
            {
                throw this.UploadFailure;
            }

            string token = $"blob:v1:payloads:{Interlocked.Increment(ref this.uploads)}";
            this.payloads[token] = payLoad;
            return token;
        }

        public override Task<string> DownloadAsync(string token, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref this.downloads);
            return Task.FromResult(this.payloads[token]);
        }

        public override bool IsKnownPayloadToken(string value)
            => value.StartsWith("blob:v1:payloads:", StringComparison.Ordinal);
    }
}
