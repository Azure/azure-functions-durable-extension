// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Grpc.Core;
using DurableTask.LargePayloadPurge;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.AzureBlobPayloads;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc.Internal;
using Moq;
using P = Microsoft.DurableTask.Protobuf.LargePayloads;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class FunctionsLargePayloadPurgeClientTests
{
    [Fact]
    public void ClientFacadeLivesInContractsAndBaseWorkerDoesNotReferenceBlobImplementation()
    {
        Assert.Same(typeof(IOrchestrationServiceLargePayloadPurgeClient).Assembly, typeof(ILargePayloadPurgeClient).Assembly);
        Assert.Equal(2, typeof(ILargePayloadPurgeClient).Assembly.GetExportedTypes().Length);
        Assert.DoesNotContain(typeof(FunctionsDurableClientProvider).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "Microsoft.DurableTask.Extensions.AzureBlobPayloads"
                || reference.Name == "Azure.Storage.Blobs");
    }

    [Fact]
    public async Task GetAndReport_PreserveOpaqueTokensDeadlinesAndCancellation()
    {
        var invoker = new Mock<CallInvoker>();
        using var cancellation = new CancellationTokenSource();
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        var response = new P.GetLargePayloadTombstonesResponse();
        response.Tombstones.Add(new P.LargePayloadTombstone { TombstoneToken = "opaque:/+/==", PayloadToken = "blob:v2:https://example/container/blob" });
        invoker.Setup(i => i.AsyncUnaryCall(
            It.IsAny<Method<P.GetLargePayloadTombstonesRequest, P.GetLargePayloadTombstonesResponse>>(),
            It.IsAny<string>(), It.Is<CallOptions>(o => o.Deadline == deadline && o.CancellationToken == cancellation.Token),
            It.Is<P.GetLargePayloadTombstonesRequest>(r => r.Limit == 42))).Returns(Call(response));
        invoker.Setup(i => i.AsyncUnaryCall(
            It.IsAny<Method<P.ReportLargePayloadPurgeResultsRequest, P.ReportLargePayloadPurgeResultsResponse>>(),
            It.IsAny<string>(), It.Is<CallOptions>(o => o.Deadline == deadline && o.CancellationToken == cancellation.Token),
            It.Is<P.ReportLargePayloadPurgeResultsRequest>(r => r.Results.Count == 1
                && r.Results[0].TombstoneToken == "opaque:/+/=="
                && (int)r.Results[0].Disposition == 3))).Returns(Call(new P.ReportLargePayloadPurgeResultsResponse()));
        var client = new FunctionsLargePayloadPurgeClient(invoker.Object);

        List<LargePayloadTombstone> tombstones = await client.GetLargePayloadTombstonesAsync(42, deadline, cancellation.Token);
        Assert.Equal("opaque:/+/==", Assert.Single(tombstones).TombstoneToken);
        await client.ReportLargePayloadPurgeResultsAsync(
            [new LargePayloadPurgeResult(tombstones[0].TombstoneToken, LargePayloadPurgeDisposition.Quarantined)],
            deadline, cancellation.Token);
        invoker.VerifyAll();
    }

    [Fact]
    public async Task Disable_OnlyForwardsSettingThroughBoundClient()
    {
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        using var cancellation = new CancellationTokenSource();
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(c => c.SetLargePayloadAutoPurgeAsync(false, cancellation.Token)).Returns(Task.CompletedTask);
        var client = new FunctionsDurableTaskClient(inner.Object, null, null);

        await client.SetLargePayloadAutoPurgeAsync(false, cancellationToken: cancellation.Token);

        inner.VerifyAll();
        inner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnsupportedStatus_IsNotRewritten()
    {
        var invoker = new Mock<CallInvoker>();
        var error = new RpcException(new Status(StatusCode.Unimplemented, "unsupported"));
        invoker.Setup(i => i.AsyncUnaryCall(
            It.IsAny<Method<P.GetLargePayloadTombstonesRequest, P.GetLargePayloadTombstonesResponse>>(),
            It.IsAny<string>(), It.IsAny<CallOptions>(), It.IsAny<P.GetLargePayloadTombstonesRequest>()))
            .Returns(new AsyncUnaryCall<P.GetLargePayloadTombstonesResponse>(
                Task.FromException<P.GetLargePayloadTombstonesResponse>(error), Task.FromResult(new Metadata()),
                () => error.Status, () => new Metadata(), () => { }));
        var client = new FunctionsLargePayloadPurgeClient(invoker.Object);
        Assert.Same(error, await Assert.ThrowsAsync<RpcException>(
            () => client.GetLargePayloadTombstonesAsync(1, DateTime.UtcNow.AddSeconds(30))));
    }

    private static AsyncUnaryCall<T> Call<T>(T response)
        => new(Task.FromResult(response), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
}
