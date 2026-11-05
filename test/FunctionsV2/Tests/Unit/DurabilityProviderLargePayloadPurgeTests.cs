// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.LargePayloadPurge.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurabilityProviderLargePayloadPurgeTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void StandaloneCapabilityUsesCanonicalSdkModels()
        {
            Type capability = typeof(IOrchestrationServiceLargePayloadPurgeClient);
            Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions", capability.Namespace);
            Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions.IOrchestrationServiceLargePayloadPurgeClient", capability.FullName);
            Assert.Null(capability.Assembly.GetType("DurableTask.LargePayloadPurge.IOrchestrationServiceLargePayloadPurgeClient"));
            Assert.Null(capability.Assembly.GetType("DurableTask.LargePayloadPurge.ILargePayloadPurgeClient"));
            Assert.Null(capability.Assembly.GetType("Microsoft.DurableTask.AzureBlobPayloads.IOrchestrationServiceLargePayloadPurgeClient"));
            Assert.Null(capability.Assembly.GetType("Microsoft.DurableTask.AzureBlobPayloads.ILargePayloadPurgeClient"));
            Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions", capability.Assembly.GetName().Name);
            Assert.Equal(typeof(LargePayloadTombstone).Assembly.GetName().Version, capability.Assembly.GetName().Version);
            Assert.Equal("6A4C0315C2D1D937", Convert.ToHexString(capability.Assembly.GetName().GetPublicKeyToken()));
            Type shared = Assert.Single(capability.GetInterfaces());
            Assert.Equal(typeof(ILargePayloadPurgeClient), shared);
            Assert.Equal("Microsoft.DurableTask.LargePayloadPurge.Abstractions", shared.Namespace);
            Assert.Same(capability.Assembly, shared.Assembly);
            Assert.Equal(nameof(IOrchestrationServiceLargePayloadPurgeClient.SetLargePayloadAutoPurgeAsync), Assert.Single(capability.GetMethods()).Name);
            Assert.Equal(2, shared.GetMethods().Length);
            Assert.Equal(
                typeof(Task<List<LargePayloadTombstone>>),
                shared.GetMethod(nameof(ILargePayloadPurgeClient.GetLargePayloadTombstonesAsync)).ReturnType);
            Assert.Equal(
                typeof(IReadOnlyList<LargePayloadPurgeResult>),
                shared.GetMethod(nameof(ILargePayloadPurgeClient.ReportLargePayloadPurgeResultsAsync)).GetParameters()[0].ParameterType);
            Assert.Equal("Microsoft.DurableTask.Client", typeof(LargePayloadTombstone).Assembly.GetName().Name);
            Assert.Same(typeof(LargePayloadTombstone).Assembly, typeof(LargePayloadPurgeResult).Assembly);
            Assert.Same(typeof(LargePayloadTombstone).Assembly, typeof(LargePayloadPurgeDisposition).Assembly);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task PlainProviderForwardsOriginalArgumentsTasksAndResults(bool enabled)
        {
            var service = new Mock<IOrchestrationService>(MockBehavior.Strict);
            var inner = new Mock<IOrchestrationServiceClient>(MockBehavior.Strict);
            Mock<IOrchestrationServiceLargePayloadPurgeClient> capability = inner.As<IOrchestrationServiceLargePayloadPurgeClient>();
            var provider = new DurabilityProvider("Test", service.Object, inner.Object, "Connection");
            var forwarder = Assert.IsAssignableFrom<IOrchestrationServiceLargePayloadPurgeClient>(provider);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            DateTime deadline = enabled ? DateTime.UtcNow.AddMinutes(1) : DateTime.MaxValue;
            var setting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tombstones = new List<LargePayloadTombstone>
            {
                new LargePayloadTombstone(" opaque:/+== ", "blob:v2:uninterpreted"),
            };
            Task<List<LargePayloadTombstone>> fetch = Task.FromResult(tombstones);
            IReadOnlyList<LargePayloadPurgeResult> results = new[]
            {
                new LargePayloadPurgeResult(tombstones[0].TombstoneToken, (LargePayloadPurgeDisposition)73),
            };
            var report = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            capability.Setup(c => c.SetLargePayloadAutoPurgeAsync(enabled, deadline, cancellation.Token)).Returns(setting.Task);
            capability.Setup(c => c.GetLargePayloadTombstonesAsync(17, deadline, cancellation.Token)).Returns(fetch);
            capability.Setup(c => c.ReportLargePayloadPurgeResultsAsync(
                It.Is<IReadOnlyList<LargePayloadPurgeResult>>(value => ReferenceEquals(value, results)), deadline, cancellation.Token))
                .Returns(report.Task);

            Assert.Same(setting.Task, forwarder.SetLargePayloadAutoPurgeAsync(enabled, deadline, cancellation.Token));
            Task<List<LargePayloadTombstone>> actualFetch =
                forwarder.GetLargePayloadTombstonesAsync(17, deadline, cancellation.Token);
            Assert.Same(fetch, actualFetch);
            Assert.Same(tombstones, await actualFetch);
            Assert.Same(report.Task, forwarder.ReportLargePayloadPurgeResultsAsync(results, deadline, cancellation.Token));
            capability.VerifyAll();
            capability.VerifyNoOtherCalls();
            service.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("Set")]
        [InlineData("Get")]
        [InlineData("Report")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void UnsupportedInnerClientDoesNotFallBackToWorkerService(string operation)
        {
            var service = new Mock<IOrchestrationService>(MockBehavior.Strict);
            service.As<IOrchestrationServiceLargePayloadPurgeClient>();
            var inner = new Mock<IOrchestrationServiceClient>(MockBehavior.Strict);
            var provider = new DurabilityProvider("Unsupported", service.Object, inner.Object, "Connection");
            var forwarder = Assert.IsAssignableFrom<IOrchestrationServiceLargePayloadPurgeClient>(provider);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => { _ = InvokeAsync(forwarder, operation); });

            Assert.Contains("large-payload purge", exception.Message);
            inner.VerifyNoOtherCalls();
            service.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("Set")]
        [InlineData("Get")]
        [InlineData("Report")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void SharedFacadeAloneDoesNotAdvertiseServiceCapability(string operation)
        {
            var inner = new Mock<IOrchestrationServiceClient>(MockBehavior.Strict);
            inner.As<ILargePayloadPurgeClient>();
            var provider = new DurabilityProvider("Unsupported", Mock.Of<IOrchestrationService>(), inner.Object, "Connection");
            var forwarder = Assert.IsAssignableFrom<IOrchestrationServiceLargePayloadPurgeClient>(provider);

            Assert.Throws<NotSupportedException>(() => { _ = InvokeAsync(forwarder, operation); });

            inner.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("Set", false)]
        [InlineData("Get", false)]
        [InlineData("Report", false)]
        [InlineData("Set", true)]
        [InlineData("Get", true)]
        [InlineData("Report", true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task InnerFailuresAreNotWrapped(string operation, bool synchronous)
        {
            var inner = new Mock<IOrchestrationServiceClient>(MockBehavior.Strict);
            Mock<IOrchestrationServiceLargePayloadPurgeClient> capability = inner.As<IOrchestrationServiceLargePayloadPurgeClient>();
            var failure = new InvalidOperationException("inner failure");
            if (synchronous)
            {
                capability.Setup(c => c.SetLargePayloadAutoPurgeAsync(true, DateTime.MaxValue, default)).Throws(failure);
                capability.Setup(c => c.GetLargePayloadTombstonesAsync(17, DateTime.MaxValue, default)).Throws(failure);
                capability.Setup(c => c.ReportLargePayloadPurgeResultsAsync(
                    It.IsAny<IReadOnlyList<LargePayloadPurgeResult>>(), DateTime.MaxValue, default)).Throws(failure);
            }
            else
            {
                capability.Setup(c => c.SetLargePayloadAutoPurgeAsync(true, DateTime.MaxValue, default)).ThrowsAsync(failure);
                capability.Setup(c => c.GetLargePayloadTombstonesAsync(17, DateTime.MaxValue, default)).ThrowsAsync(failure);
                capability.Setup(c => c.ReportLargePayloadPurgeResultsAsync(
                    It.IsAny<IReadOnlyList<LargePayloadPurgeResult>>(), DateTime.MaxValue, default)).ThrowsAsync(failure);
            }

            var provider = new DurabilityProvider("Test", Mock.Of<IOrchestrationService>(), inner.Object, "Connection");
            var forwarder = Assert.IsAssignableFrom<IOrchestrationServiceLargePayloadPurgeClient>(provider);
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(forwarder, operation)));
            Assert.Single(capability.Invocations);
        }

        private static Task InvokeAsync(IOrchestrationServiceLargePayloadPurgeClient client, string operation)
        {
            return operation switch
            {
                "Set" => client.SetLargePayloadAutoPurgeAsync(true, DateTime.MaxValue, default),
                "Get" => client.GetLargePayloadTombstonesAsync(17, DateTime.MaxValue, default),
                "Report" => client.ReportLargePayloadPurgeResultsAsync(Array.Empty<LargePayloadPurgeResult>(), DateTime.MaxValue, default),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }
    }
}
