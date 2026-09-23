// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LP = Microsoft.DurableTask.Protobuf.LargePayloads;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class LargePayloadPurgeGrpcTests
    {
        private const string TombstoneToken = " opaque:/%2F+版本\0\r\n ";
        private const string PayloadToken = " blob:v2:https://account/container/a%2Fb?sig=opaque+value ";

        private readonly ITestOutputHelper output;

        public LargePayloadPurgeGrpcTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task AllOperationsUseBindingProviderAndPreserveArguments(bool enabled)
        {
            using var fixture = new BridgeFixture(this.output);
            using var cancellation = new CancellationTokenSource();
            var bindings = new[] { ("HubA", "ConnectionA"), ("HubB", "ConnectionA"), ("HubA", "ConnectionB") };
            var providers = bindings.Select(binding => fixture.AddProvider(binding.Item1, binding.Item2)).ToArray();
            var dispositions = new[] { 0, 1, 2, 3, 73 };

            for (int i = 0; i < bindings.Length; i++)
            {
                DateTime deadline = i == 2 ? DateTime.MaxValue : DateTime.UtcNow.AddMinutes(1);
                var context = new TestCallContext(bindings[i].Item1, bindings[i].Item2, deadline, cancellation.Token);
                Mock<ILargePayloadPurgeProvider> provider = providers[i];
                IReadOnlyList<LargePayloadPurgeResult> received = null;
                provider.Setup(p => p.SetLargePayloadAutoPurgeAsync(enabled, deadline, cancellation.Token)).Returns(Task.CompletedTask);
                provider.Setup(p => p.GetLargePayloadsToPurgeAsync(17, deadline, cancellation.Token))
                    .ReturnsAsync(new[] { new LargePayloadPurgeTombstone(TombstoneToken, PayloadToken) });
                provider.Setup(p => p.ReportLargePayloadPurgeResultsAsync(It.IsAny<IReadOnlyList<LargePayloadPurgeResult>>(), deadline, cancellation.Token))
                    .Callback<IReadOnlyList<LargePayloadPurgeResult>, DateTime, CancellationToken>((results, _, _) => received = results)
                    .Returns(Task.CompletedTask);

                await fixture.Server.SetLargePayloadAutoPurge(new LP.SetLargePayloadAutoPurgeRequest { Enabled = enabled }, context);
                LP.GetLargePayloadTombstonesResponse response = await fixture.Server.GetLargePayloadTombstones(
                    new LP.GetLargePayloadTombstonesRequest { Limit = 17 }, context);
                LP.LargePayloadTombstone tombstone = Assert.Single(response.Tombstones);
                Assert.Equal(TombstoneToken, tombstone.TombstoneToken);
                Assert.Equal(PayloadToken, tombstone.PayloadToken);

                var report = new LP.ReportLargePayloadPurgeResultsRequest();
                report.Results.AddRange(dispositions.Select(disposition => new LP.LargePayloadPurgeResult
                {
                    TombstoneToken = tombstone.TombstoneToken,
                    Disposition = (LP.LargePayloadPurgeDisposition)disposition,
                }));
                await fixture.Server.ReportLargePayloadPurgeResults(report, context);

                Assert.Equal(dispositions, received.Select(result => (int)result.Disposition));
                Assert.All(received, result => Assert.Equal(TombstoneToken, result.TombstoneToken));
                provider.Verify(p => p.SetLargePayloadAutoPurgeAsync(enabled, deadline, cancellation.Token), Times.Once);
                provider.Verify(p => p.GetLargePayloadsToPurgeAsync(17, deadline, cancellation.Token), Times.Once);
                provider.Verify(p => p.ReportLargePayloadPurgeResultsAsync(It.IsAny<IReadOnlyList<LargePayloadPurgeResult>>(), deadline, cancellation.Token), Times.Once);
                provider.VerifyNoOtherCalls();
                fixture.Factory.Verify(
                    f => f.GetDurabilityProvider(It.Is<DurableClientAttribute>(
                        a => a.TaskHub == bindings[i].Item1 && a.ConnectionName == bindings[i].Item2)), Times.Exactly(3));
            }

            fixture.AssertNoDefaultOrLifecycleCalls();
        }

        [Theory]
        [InlineData("Set")]
        [InlineData("Get")]
        [InlineData("Report")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task UnsupportedBindingProviderReturnsUnimplemented(string operation)
        {
            using var fixture = new BridgeFixture(this.output);
            fixture.AddUnsupportedProvider("OtherHub", "OtherConnection");
            var context = new TestCallContext("OtherHub", "OtherConnection", DateTime.MaxValue, default);

            RpcException exception = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(fixture.Server, operation, context));

            Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
            Assert.Contains("large-payload purge", exception.Status.Detail);
            fixture.Factory.Verify(
                f => f.GetDurabilityProvider(It.Is<DurableClientAttribute>(
                    a => a.TaskHub == "OtherHub" && a.ConnectionName == "OtherConnection")), Times.Once);
            fixture.AssertNoDefaultOrLifecycleCalls();
        }

        [Theory]
        [InlineData("Set", false)]
        [InlineData("Get", false)]
        [InlineData("Report", false)]
        [InlineData("Set", true)]
        [InlineData("Get", true)]
        [InlineData("Report", true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ProviderFailuresAndCancellationAreNotReplaced(string operation, bool canceled)
        {
            using var fixture = new BridgeFixture(this.output);
            using var cancellation = new CancellationTokenSource();
            if (canceled)
            {
                cancellation.Cancel();
            }

            DateTime deadline = DateTime.UtcNow.AddMinutes(1);
            var context = new TestCallContext("Hub", "Connection", deadline, cancellation.Token);
            Mock<ILargePayloadPurgeProvider> provider = fixture.AddProvider("Hub", "Connection");
            Exception failure = canceled
                ? new OperationCanceledException(cancellation.Token)
                : new RpcException(new Status(StatusCode.ResourceExhausted, "backend failure"), new Metadata { { "failure-id", "opaque" } });
            provider.Setup(p => p.SetLargePayloadAutoPurgeAsync(true, deadline, cancellation.Token)).ThrowsAsync(failure);
            provider.Setup(p => p.GetLargePayloadsToPurgeAsync(17, deadline, cancellation.Token)).ThrowsAsync(failure);
            provider.Setup(p => p.ReportLargePayloadPurgeResultsAsync(It.IsAny<IReadOnlyList<LargePayloadPurgeResult>>(), deadline, cancellation.Token)).ThrowsAsync(failure);

            Exception actual = await Assert.ThrowsAnyAsync<Exception>(() => InvokeAsync(fixture.Server, operation, context));

            Assert.Same(failure, actual);
            Assert.Single(provider.Invocations);
            fixture.AssertNoDefaultOrLifecycleCalls();
        }

        [Theory]
        [InlineData(0, LargePayloadPurgeDisposition.Unspecified)]
        [InlineData(1, LargePayloadPurgeDisposition.Deleted)]
        [InlineData(2, LargePayloadPurgeDisposition.Retry)]
        [InlineData(3, LargePayloadPurgeDisposition.Quarantined)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DispositionsMatchWireValues(int wireValue, LargePayloadPurgeDisposition disposition)
        {
            Assert.Equal(wireValue, (int)disposition);
            Assert.Equal(disposition.ToString(), ((LP.LargePayloadPurgeDisposition)wireValue).ToString());
        }

        [Theory]
        [InlineData(TestGrpcListenerMode.Legacy)]
        [InlineData(TestGrpcListenerMode.AspNetCore)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task LocalListenerBindsAllOperationsAndPreservesBackendStatus(TestGrpcListenerMode mode)
        {
            using var fixture = new BridgeFixture(this.output);
            Mock<ILargePayloadPurgeProvider> provider = fixture.AddProvider("WireHub", "WireConnection");
            provider.Setup(p => p.SetLargePayloadAutoPurgeAsync(true, DateTime.MaxValue, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
            provider.Setup(p => p.GetLargePayloadsToPurgeAsync(17, DateTime.MaxValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new LargePayloadPurgeTombstone(TombstoneToken, PayloadToken) }).Verifiable();
            provider.Setup(p => p.ReportLargePayloadPurgeResultsAsync(
                It.Is<IReadOnlyList<LargePayloadPurgeResult>>(r => r.Count == 1 && r[0].TombstoneToken == TombstoneToken && r[0].Disposition == LargePayloadPurgeDisposition.Retry),
                DateTime.MaxValue,
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
            provider.Setup(p => p.SetLargePayloadAutoPurgeAsync(false, DateTime.MaxValue, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RpcException(new Status(StatusCode.FailedPrecondition, "backend rejected setting"), new Metadata { { "failure-id", "opaque" } })).Verifiable();
            ILocalGrpcListener listener = LocalGrpcListener.Create(fixture.Extension, (LocalGrpcListenerMode)mode);

            try
            {
                await listener.StartAsync(default);
                using GrpcChannel channel = GrpcChannel.ForAddress(listener.ListenAddress);
                var client = new LP.LargePayloadPurge.LargePayloadPurgeClient(channel);
                var sidecar = new P.TaskHubSidecarService.TaskHubSidecarServiceClient(channel);
                await sidecar.HelloAsync(new Google.Protobuf.WellKnownTypes.Empty());
                Assert.Equal("microsoft.durabletask.largepayloads.LargePayloadPurge", LP.LargePayloadPurge.Descriptor.FullName);
                var headers = new Metadata { { "Durable-TaskHub", "WireHub" }, { "Durable-ConnectionName", "WireConnection" } };
                await client.SetLargePayloadAutoPurgeAsync(new LP.SetLargePayloadAutoPurgeRequest { Enabled = true }, headers);
                LP.GetLargePayloadTombstonesResponse response = await client.GetLargePayloadTombstonesAsync(new LP.GetLargePayloadTombstonesRequest { Limit = 17 }, headers);
                LP.LargePayloadTombstone tombstone = Assert.Single(response.Tombstones);
                Assert.Equal(TombstoneToken, tombstone.TombstoneToken);
                Assert.Equal(PayloadToken, tombstone.PayloadToken);
                var report = new LP.ReportLargePayloadPurgeResultsRequest();
                report.Results.Add(new LP.LargePayloadPurgeResult { TombstoneToken = tombstone.TombstoneToken, Disposition = LP.LargePayloadPurgeDisposition.Retry });
                await client.ReportLargePayloadPurgeResultsAsync(report, headers);

                RpcException exception = await Assert.ThrowsAsync<RpcException>(async () =>
                    await client.SetLargePayloadAutoPurgeAsync(new LP.SetLargePayloadAutoPurgeRequest { Enabled = false }, headers));
                Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
                Assert.Equal("backend rejected setting", exception.Status.Detail);
                Assert.Equal("opaque", exception.Trailers.GetValue("failure-id"));
                provider.Verify();
                provider.VerifyNoOtherCalls();
                fixture.Factory.Verify(
                    f => f.GetDurabilityProvider(It.Is<DurableClientAttribute>(
                        a => a.TaskHub == "WireHub" && a.ConnectionName == "WireConnection")), Times.Exactly(4));
                fixture.AssertNoDefaultOrLifecycleCalls();
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        [Theory]
        [InlineData(TestGrpcListenerMode.Legacy, "Set")]
        [InlineData(TestGrpcListenerMode.Legacy, "Get")]
        [InlineData(TestGrpcListenerMode.Legacy, "Report")]
        [InlineData(TestGrpcListenerMode.AspNetCore, "Set")]
        [InlineData(TestGrpcListenerMode.AspNetCore, "Get")]
        [InlineData(TestGrpcListenerMode.AspNetCore, "Report")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task LocalListenerUsesExistingExceptionInterceptor(TestGrpcListenerMode mode, string operation)
        {
            using var fixture = new BridgeFixture(this.output);
            Mock<ILargePayloadPurgeProvider> provider = fixture.AddProvider("FailureHub", "FailureConnection");
            var failure = new InvalidOperationException("Purge provider failure.");
            provider.Setup(p => p.SetLargePayloadAutoPurgeAsync(true, DateTime.MaxValue, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
            provider.Setup(p => p.GetLargePayloadsToPurgeAsync(17, DateTime.MaxValue, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
            provider.Setup(p => p.ReportLargePayloadPurgeResultsAsync(It.IsAny<IReadOnlyList<LargePayloadPurgeResult>>(), DateTime.MaxValue, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
            ILocalGrpcListener listener = LocalGrpcListener.Create(fixture.Extension, (LocalGrpcListenerMode)mode);

            try
            {
                await listener.StartAsync(default);
                using GrpcChannel channel = GrpcChannel.ForAddress(listener.ListenAddress);
                var client = new LP.LargePayloadPurge.LargePayloadPurgeClient(channel);
                var headers = new Metadata { { "Durable-TaskHub", "FailureHub" }, { "Durable-ConnectionName", "FailureConnection" } };
                RpcException exception = await Assert.ThrowsAsync<RpcException>(() => InvokeAsync(client, operation, headers));

                Assert.Equal(StatusCode.Unknown, exception.StatusCode);
                Assert.Single(provider.Invocations);
                LogMessage warning = Assert.Single(
                    fixture.LoggerProvider.GetAllLogMessages(),
                    message => message.Level == LogLevel.Warning && message.FormattedMessage.Contains(failure.Message));
                Assert.Contains("/microsoft.durabletask.largepayloads.LargePayloadPurge/" + operation, warning.FormattedMessage);
                Assert.Contains(nameof(InvalidOperationException), warning.FormattedMessage);
                Assert.Equal("FailureHub", warning.State.Single(pair => pair.Key == "hubName").Value);
                fixture.AssertNoDefaultOrLifecycleCalls();
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        private static Task InvokeAsync(LP.LargePayloadPurge.LargePayloadPurgeClient client, string operation, Metadata headers)
        {
            return operation switch
            {
                "Set" => client.SetLargePayloadAutoPurgeAsync(new LP.SetLargePayloadAutoPurgeRequest { Enabled = true }, headers).ResponseAsync,
                "Get" => client.GetLargePayloadTombstonesAsync(new LP.GetLargePayloadTombstonesRequest { Limit = 17 }, headers).ResponseAsync,
                "Report" => client.ReportLargePayloadPurgeResultsAsync(new LP.ReportLargePayloadPurgeResultsRequest(), headers).ResponseAsync,
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }

        private static Task InvokeAsync(LargePayloadPurgeGrpcServer server, string operation, ServerCallContext context)
        {
            return operation switch
            {
                "Set" => server.SetLargePayloadAutoPurge(new LP.SetLargePayloadAutoPurgeRequest { Enabled = true }, context),
                "Get" => server.GetLargePayloadTombstones(new LP.GetLargePayloadTombstonesRequest { Limit = 17 }, context),
                "Report" => server.ReportLargePayloadPurgeResults(new LP.ReportLargePayloadPurgeResultsRequest(), context),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }

        private sealed class BridgeFixture : IDisposable
        {
            private readonly Dictionary<(string Hub, string Connection), DurabilityProvider> providers = new Dictionary<(string, string), DurabilityProvider>();
            private readonly Mock<IOrchestrationService> service = new Mock<IOrchestrationService>();
            private readonly Mock<IOrchestrationServiceClient> serviceClient = new Mock<IOrchestrationServiceClient>();
            private readonly Mock<DurabilityProvider> defaultProvider;
            private readonly LoggerFactory loggerFactory;

            public BridgeFixture(ITestOutputHelper output)
            {
                this.LoggerProvider = new TestLoggerProvider(output);
                this.defaultProvider = this.CreateProvider();
                this.defaultProvider.As<ILargePayloadPurgeProvider>();
                this.Factory.SetupGet(f => f.Name).Returns(AzureStorageDurabilityProviderFactory.ProviderName);
                this.Factory.Setup(f => f.GetDurabilityProvider()).Returns(this.defaultProvider.Object);
                this.Factory.Setup(f => f.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()))
                    .Returns<DurableClientAttribute>(a => this.providers[(a.TaskHub, a.ConnectionName)]);
                this.loggerFactory = new LoggerFactory(new[] { this.LoggerProvider });
                this.Extension = new DurableTaskExtension(
                    new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions { HubName = "DefaultHub" }),
                    this.loggerFactory,
                    TestHelpers.GetTestNameResolver(),
                    new[] { this.Factory.Object },
                    new TestHostShutdownNotificationService(),
                    new DurableHttpMessageHandlerFactory(),
                    platformInformationService: TestHelpers.GetMockPlatformInformationService(language: WorkerRuntimeType.DotNet));
                this.Server = new LargePayloadPurgeGrpcServer(this.Extension);
                this.Factory.Invocations.Clear();
                this.defaultProvider.Invocations.Clear();
                this.service.Invocations.Clear();
                this.serviceClient.Invocations.Clear();
            }

            public Mock<IDurabilityProviderFactory> Factory { get; } = new Mock<IDurabilityProviderFactory>();

            public TestLoggerProvider LoggerProvider { get; }

            public DurableTaskExtension Extension { get; }

            public LargePayloadPurgeGrpcServer Server { get; }

            public Mock<ILargePayloadPurgeProvider> AddProvider(string hub, string connection)
            {
                Mock<DurabilityProvider> provider = this.CreateProvider();
                Mock<ILargePayloadPurgeProvider> purgeProvider = provider.As<ILargePayloadPurgeProvider>();
                this.providers.Add((hub, connection), provider.Object);
                return purgeProvider;
            }

            public void AddUnsupportedProvider(string hub, string connection)
            {
                this.providers.Add((hub, connection), this.CreateProvider().Object);
            }

            public void AssertNoDefaultOrLifecycleCalls()
            {
                this.Factory.Verify(f => f.GetDurabilityProvider(), Times.Never);
                this.defaultProvider.VerifyNoOtherCalls();
                this.service.VerifyNoOtherCalls();
                this.serviceClient.VerifyNoOtherCalls();
            }

            public void Dispose()
            {
                this.Extension.Dispose();
                this.loggerFactory.Dispose();
            }

            private Mock<DurabilityProvider> CreateProvider()
            {
                var provider = new Mock<DurabilityProvider>("Test", this.service.Object, this.serviceClient.Object, "TestConnection") { CallBase = true };
                provider.Setup(p => p.SetUseSeparateQueueForEntityWorkItems(It.IsAny<bool>()));
                return provider;
            }
        }

        private sealed class TestCallContext : ServerCallContext
        {
            public TestCallContext(string hub, string connection, DateTime deadline, CancellationToken cancellationToken)
            {
                this.RequestHeadersCore = new Metadata { { "Durable-TaskHub", hub }, { "Durable-ConnectionName", connection } };
                this.DeadlineCore = deadline;
                this.CancellationTokenCore = cancellationToken;
            }

            protected override string MethodCore => "test";

            protected override string HostCore => "localhost";

            protected override string PeerCore => "test";

            protected override DateTime DeadlineCore { get; }

            protected override Metadata RequestHeadersCore { get; }

            protected override CancellationToken CancellationTokenCore { get; }

            protected override Metadata ResponseTrailersCore { get; } = new Metadata();

            protected override Status StatusCore { get; set; }

            protected override WriteOptions WriteOptionsCore { get; set; }

            protected override AuthContext AuthContextCore => throw new NotSupportedException();

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) => throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }
    }
}
