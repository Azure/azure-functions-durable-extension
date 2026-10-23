// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Grpc.Core;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Grpc.Internal;
using Microsoft.DurableTask.LargePayloadPurge.Abstractions;
using Moq;
using P = Microsoft.DurableTask.Protobuf.LargePayloads;

namespace Microsoft.Azure.Functions.Worker.Tests;

public class FunctionsLargePayloadPurgeClientTests
{
    [Fact]
    public void ClientFacadeLivesInContractsAndBaseWorkerDoesNotReferenceBlobImplementation()
    {
        Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions", typeof(IOrchestrationServiceLargePayloadPurgeClient).Namespace);
        Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions.IOrchestrationServiceLargePayloadPurgeClient",
            typeof(IOrchestrationServiceLargePayloadPurgeClient).FullName);
        Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions", typeof(ILargePayloadPurgeClient).Namespace);
        Assert.Null(typeof(ILargePayloadPurgeClient).Assembly.GetType("DurableTask.LargePayloadPurge.IOrchestrationServiceLargePayloadPurgeClient"));
        Assert.Null(typeof(ILargePayloadPurgeClient).Assembly.GetType("DurableTask.LargePayloadPurge.ILargePayloadPurgeClient"));
        Assert.Null(typeof(ILargePayloadPurgeClient).Assembly.GetType("Microsoft.DurableTask.AzureBlobPayloads.IOrchestrationServiceLargePayloadPurgeClient"));
        Assert.Null(typeof(ILargePayloadPurgeClient).Assembly.GetType("Microsoft.DurableTask.AzureBlobPayloads.ILargePayloadPurgeClient"));
        Assert.Same(typeof(IOrchestrationServiceLargePayloadPurgeClient).Assembly, typeof(ILargePayloadPurgeClient).Assembly);
        Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions", typeof(ILargePayloadPurgeClient).Assembly.GetName().Name);
        Assert.Equal(typeof(LargePayloadTombstone).Assembly.GetName().Version, typeof(ILargePayloadPurgeClient).Assembly.GetName().Version);
        Assert.Equal("6A4C0315C2D1D937", Convert.ToHexString(typeof(ILargePayloadPurgeClient).Assembly.GetName().GetPublicKeyToken()));
        Assert.Equal(typeof(ILargePayloadPurgeClient), Assert.Single(typeof(IOrchestrationServiceLargePayloadPurgeClient).GetInterfaces()));
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
        var readiness = new TestReadinessValidator();
        using var cancellation = new CancellationTokenSource();
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(c => c.SetLargePayloadAutoPurgeAsync(false, cancellation.Token)).Returns(Task.CompletedTask);
        var client = new FunctionsDurableTaskClient(inner.Object, null, null, readinessValidator: readiness);

        await client.SetLargePayloadAutoPurgeAsync(false, cancellationToken: cancellation.Token);

        inner.VerifyAll();
        Assert.Equal(0, readiness.CallCount);
        inner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Enable_ValidatesBeforeForwardingSetting()
    {
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        var calls = new List<string>();
        var readiness = new TestReadinessValidator(
            cancellation => { calls.Add("validate"); return Task.CompletedTask; });
        using var cancellation = new CancellationTokenSource();
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(c => c.SetLargePayloadAutoPurgeAsync(true, cancellation.Token))
            .Callback(() => calls.Add("rpc"))
            .Returns(Task.CompletedTask);
        var client = new FunctionsDurableTaskClient(inner.Object, null, null, readinessValidator: readiness);

        await ((ILargePayloadAutoPurgeClient)client).SetLargePayloadAutoPurgeAsync(true, cancellation.Token);

        Assert.Equal(new[] { "validate", "rpc" }, calls);
        inner.VerifyAll();
    }

    [Fact]
    public async Task Enable_ValidationFailureDoesNotCallInnerClient()
    {
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>();
        var failure = new InvalidOperationException("Local payload store is not ready.");
        var readiness = new TestReadinessValidator(_ => Task.FromException(failure));
        var client = new FunctionsDurableTaskClient(inner.Object, null, null, readinessValidator: readiness);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(
            () => ((ILargePayloadAutoPurgeClient)client).SetLargePayloadAutoPurgeAsync(true, CancellationToken.None)));

        Assert.Equal(1, readiness.CallCount);
        inner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Enable_WithoutOptionalReadinessHookRetainsControlOnlyBehavior()
    {
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        inner.As<ILargePayloadAutoPurgeClient>()
            .Setup(c => c.SetLargePayloadAutoPurgeAsync(true, CancellationToken.None)).Returns(Task.CompletedTask);
        var client = new FunctionsDurableTaskClient(inner.Object, null, null);

        await ((ILargePayloadAutoPurgeClient)client).SetLargePayloadAutoPurgeAsync(true, CancellationToken.None);

        inner.VerifyAll();
    }

    [Fact]
    public async Task OrdinaryClientOperationDoesNotValidatePayloadStore()
    {
        var inner = new Mock<DurableTaskClient>(MockBehavior.Strict, "test");
        var readiness = new TestReadinessValidator();
        inner.Setup(c => c.GetInstancesAsync("instance", false, CancellationToken.None))
            .ReturnsAsync((OrchestrationMetadata?)null);
        var client = new FunctionsDurableTaskClient(inner.Object, null, null, readinessValidator: readiness);

        Assert.Null(await client.GetInstancesAsync("instance"));

        inner.VerifyAll();
        Assert.Equal(0, readiness.CallCount);
    }

    [Fact]
    public async Task UnsupportedStatus_IsNotRewritten()
    {
        var invoker = new Mock<CallInvoker>();
        var error = new RpcException(new Status(StatusCode.Unimplemented, "unsupported"));
        using var call = new AsyncUnaryCall<P.GetLargePayloadTombstonesResponse>(
            Task.FromException<P.GetLargePayloadTombstonesResponse>(error), Task.FromResult(new Metadata()),
            () => error.Status, () => new Metadata(), () => { });
        invoker.Setup(i => i.AsyncUnaryCall(
            It.IsAny<Method<P.GetLargePayloadTombstonesRequest, P.GetLargePayloadTombstonesResponse>>(),
            It.IsAny<string>(), It.IsAny<CallOptions>(), It.IsAny<P.GetLargePayloadTombstonesRequest>()))
            .Returns(call);
        var client = new FunctionsLargePayloadPurgeClient(invoker.Object);
        Assert.Same(error, await Assert.ThrowsAsync<RpcException>(
            () => client.GetLargePayloadTombstonesAsync(1, DateTime.UtcNow.AddSeconds(30))));
    }

    private static AsyncUnaryCall<T> Call<T>(T response)
        => new(Task.FromResult(response), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });

    private sealed class TestReadinessValidator : ILargePayloadAutoPurgeReadinessValidator
    {
        private readonly Func<CancellationToken, Task> validate;

        public TestReadinessValidator(Func<CancellationToken, Task>? validate = null)
        {
            this.validate = validate ?? (_ => Task.CompletedTask);
        }

        public int CallCount { get; private set; }

        public Task ValidateAsync(CancellationToken cancellation)
        {
            this.CallCount++;
            return this.validate(cancellation);
        }
    }
}
