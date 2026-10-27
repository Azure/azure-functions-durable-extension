// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Grpc;
using Microsoft.Extensions.DependencyInjection;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class ExternalizedSchedulingTests
{
    [Theory]
    [InlineData("object")]
    [InlineData("string")]
    [InlineData("array")]
    [InlineData("binary")]
    public async Task Scheduling_ExternalizesArgument_NotHostEnvelope(string inputType)
    {
        object input = inputType switch
        {
            "object" => new { Data = new string('x', 1_100_000) },
            "string" => new string('x', 1_100_000),
            "array" => new[] { new string('x', 550_000), new string('y', 550_000) },
            "binary" => new byte[1_100_000],
            _ => throw new ArgumentOutOfRangeException(nameof(inputType)),
        };
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        P.OrchestratorResponse response = Schedule("PayloadActivity", input);

        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);

        string hostInput = Assert.Single(response.Actions).ScheduleTask.Input;
        using JsonDocument envelope = JsonDocument.Parse(hostInput);
        Assert.Equal(JsonValueKind.Array, envelope.RootElement.ValueKind);
        Assert.Equal(1, envelope.RootElement.GetArrayLength());
        string reference = envelope.RootElement[0].GetRawText();
        Assert.True(store.IsKnownPayloadToken(envelope.RootElement[0].GetString()!));
        Assert.True(hostInput.Length < 256);
        Assert.Equal(
            new DurableTaskWorkerOptions().DataConverter.Serialize(input),
            await payloads.ResolveAsync(reference, CancellationToken.None));
        Assert.Equal(1, store.Uploads);
    }

    [Theory]
    [InlineData("BuiltIn::HttpActivity")]
    [InlineData("bUiLtIn::HttpActivity")]
    public async Task Scheduling_HostOwnedActivity_IsNotExternalizedOrSizeLimited(string name)
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store, threshold: 256, maximum: 512);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        P.OrchestratorResponse response = Schedule(name, new { Content = new string('x', 1024) });
        byte[] original = response.ToByteArray();

        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);

        Assert.Equal(original, response.ToByteArray());
        Assert.Equal(0, store.Uploads);
    }

    [Fact]
    public async Task Scheduling_KnownReference_RetainsEnvelopeWithoutAnotherUpload()
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        string token = await store.UploadAsync("\"original\"", CancellationToken.None);
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store, threshold: 1);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        P.OrchestratorResponse response = Schedule("PayloadActivity", token);
        byte[] original = response.ToByteArray();

        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);
        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);

        Assert.Equal(original, response.ToByteArray());
        Assert.Equal(1, store.Uploads);
    }

    [Fact]
    public async Task ScheduledHistory_ExplicitRead_HydratesArgumentInsideEnvelope()
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        string data = JsonSerializer.Serialize(new string('x', 1_100_000));
        string? reference = await payloads.ExternalizeAsync(data, CancellationToken.None);
        var history = new P.HistoryChunk
        {
            Events =
            {
                new P.HistoryEvent { TaskScheduled = new P.TaskScheduledEvent { Name = "PayloadActivity", Input = $"[{reference}]" } },
            },
        };

        await ExternalizedPayloads.TransformAsync(history, payloads, externalize: false, CancellationToken.None);

        Assert.Equal($"[{data}]", history.Events[0].TaskScheduled.Input);
        Assert.Equal(1, store.Downloads);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("small")]
    public async Task Scheduling_InlineArgument_IsUnchanged(string? input)
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        P.OrchestratorResponse response = Schedule("PayloadActivity", input);
        byte[] original = response.ToByteArray();

        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);

        Assert.Equal(original, response.ToByteArray());
        Assert.Equal(0, store.Uploads);
    }

    [Theory]
    [InlineData(255, 0)]
    [InlineData(256, 1)]
    public async Task Scheduling_ThresholdMeasuresSerializedArgument_NotEnvelope(int argumentBytes, int expectedUploads)
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store, threshold: 256);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        P.OrchestratorResponse response = Schedule("PayloadActivity", new string('x', argumentBytes - 2));

        await ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);

        Assert.Equal(expectedUploads, store.Uploads);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(513)]
    public async Task Scheduling_MaximumMeasuresSerializedArgument_NotEnvelope(int argumentBytes)
    {
        var store = new ExternalizedPayloadsTests.MemoryPayloadStore();
        using ServiceProvider services = ExternalizedPayloadsTests.CreateServices(store, threshold: 256, maximum: 512);
        IExternalizedPayloads payloads = services.GetRequiredService<IExternalizedPayloads>();
        P.OrchestratorResponse response = Schedule("PayloadActivity", new string('x', argumentBytes - 2));

        Task transform = ExternalizedPayloads.TransformAsync(response, payloads, externalize: true, CancellationToken.None);
        if (argumentBytes > 512)
        {
            await Assert.ThrowsAsync<Microsoft.DurableTask.PayloadStorageException>(() => transform);
            Assert.Equal(0, store.Uploads);
        }
        else
        {
            await transform;
            Assert.Equal(1, store.Uploads);
        }
    }

    private static P.OrchestratorResponse Schedule(string name, object? input)
    {
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
                        OrchestrationInstance = new P.OrchestrationInstance
                        {
                            InstanceId = "instance",
                            ExecutionId = "execution",
                        },
                    },
                },
            },
        };
        string output = GrpcOrchestrationRunner.LoadAndRun<object?, string>(
            Convert.ToBase64String(request.ToByteArray()),
            (context, _) => context.CallActivityAsync<string?>(name, input));
        return P.OrchestratorResponse.Parser.ParseFrom(Convert.FromBase64String(output));
    }
}
