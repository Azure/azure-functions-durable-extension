// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class AzureStorageDurabilityProviderFactoryTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultWorkerId_IsMachineName()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal(Environment.MachineName, settings.WorkerId);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsAreApplied()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: true));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal(128, settings.ControlQueueBufferThreshold);
            Assert.Equal(5, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(10, settings.MaxConcurrentTaskActivityWorkItems);
            Assert.Equal(25, settings.MaxStorageOperationConcurrency);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsForPythonAreApplied()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: true, language: WorkerRuntimeType.Python));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal(32, settings.ControlQueueBufferThreshold);
            Assert.Equal(5, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(10, settings.MaxConcurrentTaskActivityWorkItems);
            Assert.Equal(25, settings.MaxStorageOperationConcurrency);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsAreNotAlwaysApplied()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: false));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            // We want to make sure that the consumption defaults (listed below)
            // aren't applied on non-consumption plans.
            Assert.NotEqual(32, settings.ControlQueueBufferThreshold);
            Assert.NotEqual(5, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.NotEqual(10, settings.MaxConcurrentTaskActivityWorkItems);
            Assert.NotEqual(25, settings.MaxStorageOperationConcurrency);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ConsumptionDefaultsDoNotOverrideCustomerOptions()
        {
            var storageAccountProvider = new TestStorageServiceClientProviderFactory();
            var options = new DurableTaskOptions();

            options.StorageProvider.Add("ControlQueueBufferThreshold", 999);
            options.MaxConcurrentOrchestratorFunctions = 888;
            options.MaxConcurrentActivityFunctions = 777;

            var mockOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var nameResolver = new Mock<INameResolver>().Object;
            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                storageAccountProvider,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(inConsumption: true));

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            // We want to make sure that the consumption defaults (listed below)
            // aren't applied on non-consumption plans.
            Assert.Equal(999, settings.ControlQueueBufferThreshold);
            Assert.Equal(888, settings.MaxConcurrentTaskOrchestrationWorkItems);
            Assert.Equal(777, settings.MaxConcurrentTaskActivityWorkItems);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EnvironmentIsVMSS_WorkerIdFromEnvironmentVariables()
        {
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { "WEBSITE_CURRENT_STAMPNAME", "waws-prod-euapbn1-003" },
                { "RoleInstanceId", "dw0SmallDedicatedWebWorkerRole_hr0HostRole-3-VM-13" },
            });

            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var settings = factory.GetAzureStorageOrchestrationServiceSettings();

            Assert.Equal("waws-prod-euapbn1-003:dw0SmallDedicatedWebWorkerRole_hr0HostRole-3-VM-13", settings.WorkerId);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CustomConnectionNameIsResolved()
        {
            var storageAccountProvider = new CustomTestStorageAccountProvider("CustomConnection");
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;

            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                storageAccountProvider,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            factory.GetDurabilityProvider(); // This will initialize the default connection string
            var provider = factory.GetDurabilityProvider(new DurableClientAttribute() { ConnectionName = "CustomConnection", TaskHub = "TestHubName" });

            Assert.Equal("CustomConnection", provider.ConnectionName);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DefaultConnectionNameIsResolved()
        {
            var storageAccountProvider = new CustomTestStorageAccountProvider("CustomConnection");
            var mockOptions = new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions());
            var nameResolver = new Mock<INameResolver>().Object;

            var factory = new AzureStorageDurabilityProviderFactory(
                mockOptions,
                storageAccountProvider,
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            var provider = factory.GetDurabilityProvider();

            Assert.Equal("Storage", provider.ConnectionName);
        }
    }
}
