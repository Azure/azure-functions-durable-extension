// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Core;
using FluentAssertions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationClientBaseTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task StartNewAsync_is_calling_overload_method()
        {
            var instanceId = Guid.NewGuid().ToString();
            const string functionName = "sampleFunction";
            var durableOrchestrationClientBaseMock = new Mock<IDurableOrchestrationClient> { CallBase = true };
            durableOrchestrationClientBaseMock.Setup(x => x.StartNewAsync(functionName, string.Empty, null)).ReturnsAsync(instanceId);

            var result = await durableOrchestrationClientBaseMock.Object.StartNewAsync(functionName, null);
            result.Should().Be(instanceId);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RaiseEventAsync_InvalidInstanceId_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(GetInvalidInstanceState());
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableOrchestrationClient(orchestrationServiceClientMock.Object, GetDurableTaskExtension(), new OrchestrationClientAttribute { });

            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await durableOrchestrationClient.RaiseEventAsync("invalid_instance_id", "anyEvent", new { message = "any message" }));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RaiseEventAsync_NonRunningFunction_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Completed));
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableOrchestrationClient(orchestrationServiceClientMock.Object, GetDurableTaskExtension(), new OrchestrationClientAttribute { });

            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await durableOrchestrationClient.RaiseEventAsync("valid_instance_id", "anyEvent", new { message = "any message" }));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TerminateAsync_InvalidInstanceId_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInvalidInstanceState());
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableOrchestrationClient(orchestrationServiceClientMock.Object, GetDurableTaskExtension(), new OrchestrationClientAttribute { });

            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await durableOrchestrationClient.TerminateAsync("invalid_instance_id", "any reason"));
            orchestrationServiceClientMock.Verify(x => x.ForceTerminateTaskOrchestrationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TerminateAsync_RunningOrchestrator_TerminateEventPlaced()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableOrchestrationClient(orchestrationServiceClientMock.Object, GetDurableTaskExtension(), new OrchestrationClientAttribute { });

            await durableOrchestrationClient.TerminateAsync("valid_instance_id", "any reason");
            orchestrationServiceClientMock.Verify(x => x.ForceTerminateTaskOrchestrationAsync("valid_instance_id", "any reason"), Times.Once());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TerminateAsync_NonRunningOrchestrator_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Completed));
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableOrchestrationClient(orchestrationServiceClientMock.Object, GetDurableTaskExtension(), new OrchestrationClientAttribute { });

            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await durableOrchestrationClient.TerminateAsync("invalid_instance_id", "any reason"));
            orchestrationServiceClientMock.Verify(x => x.ForceTerminateTaskOrchestrationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        private static List<OrchestrationState> GetInvalidInstanceState()
        {
            return null;
        }

        private static List<OrchestrationState> GetInstanceState(OrchestrationStatus status)
        {
            return new List<OrchestrationState>()
            {
                new OrchestrationState()
                {
                    OrchestrationInstance = new OrchestrationInstance
                    {
                        InstanceId = "valid_instance_id",
                    },
                    OrchestrationStatus = status,
                },
            };
        }

        private static DurableTaskExtension GetDurableTaskExtension()
        {
            var options = new DurableTaskOptions();
            options.HubName = "DurableTaskHub";
            options.StorageProvider = new StorageProviderOptions
            {
                AzureStorage = new AzureStorageOptions(),
            };
            IOptions<DurableTaskOptions> wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var connectionStringResolver = new TestConnectionStringResolver();
            return new DurableTaskExtension(
                wrappedOptions,
                new LoggerFactory(),
                TestHelpers.GetTestNameResolver(),
                new OrchestrationServiceFactory(wrappedOptions, connectionStringResolver));
        }
    }
}
