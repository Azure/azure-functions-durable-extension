// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class LargePayloadPurgeRegistrationTests
    {
        [Theory]
        [InlineData("Customer", WorkerRuntimeType.DotNetIsolated)]
        [InlineData("blobpurgejoborchestrator", WorkerRuntimeType.DotNetIsolated)]
        [InlineData("BlobPurgeJobOrchestrator.Suffix", WorkerRuntimeType.DotNetIsolated)]
        [InlineData("BlobPurgeJobOrchestrator", WorkerRuntimeType.DotNet)]
        [InlineData("BlobPurgeJobOrchestrator", WorkerRuntimeType.Python)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Marker_RejectsNonSdkNamesAndOtherRuntimes(string name, WorkerRuntimeType runtime)
        {
            using var extension = CreateExtension(runtime);
            Assert.Throws<InvalidOperationException>(() => extension.RegisterLargePayloadPurgeOrchestration(name));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Marker_RequiresIndexingBeforeWorkerInitialization()
        {
            using var extension = CreateExtension(WorkerRuntimeType.DotNetIsolated);
            extension.RegisterLargePayloadPurgeOrchestration("BlobPurgeJobOrchestrator");
            extension.EnsureTaskHubWorker();
            Assert.Throws<InvalidOperationException>(() => extension.RegisterLargePayloadPurgeOrchestration("BlobPurgeJobOrchestrator"));
            ((TestHostShutdownNotificationService)extension.HostLifetimeService).SignalShutdown();
        }

        private static DurableTaskExtension CreateExtension(WorkerRuntimeType runtime)
        {
            var options = new DurableTaskOptions
            {
                LocalRpcEndpointEnabled = false,
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };
            options.StorageProvider["type"] = "Emulator";
            var platform = TestHelpers.GetMockPlatformInformationService();
            Mock.Get(platform).Setup(p => p.GetWorkerRuntimeType()).Returns(runtime);
            return new DurableTaskExtension(
                new OptionsWrapper<DurableTaskOptions>(options),
                NullLoggerFactory.Instance,
                TestHelpers.GetTestNameResolver(),
                new[] { new EmulatorDurabilityProviderFactory() },
                new TestHostShutdownNotificationService(),
                platformInformationService: platform);
        }
    }
}
