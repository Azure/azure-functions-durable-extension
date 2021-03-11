﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Moq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class StorageProviderSelectionTests
    {
        public StorageProviderSelectionTests() { }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("AzureStorage")]
        public void SelectingAzureStorageStorageProvider(string storageProvider)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            Mock<IDurabilityProviderFactory> azureStorageMock = GetAzureStorageStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> microsoftSQLMock = GetMicrosoftSQLStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> netheriteMock = GetNetheriteStorageProviderMock(orchestrationServiceClientMock);

            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = new[] { azureStorageMock.Object, microsoftSQLMock.Object, netheriteMock.Object };

            DurableTaskOptions options = new DurableTaskOptions();
            options.StorageProvider["type"] = storageProvider;

            using (ITestHost host = TestHelpers.GetJobHostWithMultipleDurabilityProviders(
                options: options,
                durabilityProviderFactories: durabilityProviderFactories))
            {
                azureStorageMock.Verify(a => a.GetDurabilityProvider(), Times.Once());
                netheriteMock.Verify(n => n.GetDurabilityProvider(), Times.Never());
                microsoftSQLMock.Verify(m => m.GetDurabilityProvider(), Times.Never());
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("Netherite")]
        public void SelectingNetheriteStorageProvider(string storageProvider)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            Mock<IDurabilityProviderFactory> azureStorageMock = GetAzureStorageStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> microsoftSQLMock = GetMicrosoftSQLStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> netheriteMock = GetNetheriteStorageProviderMock(orchestrationServiceClientMock);

            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = new[] { azureStorageMock.Object, microsoftSQLMock.Object, netheriteMock.Object };

            DurableTaskOptions options = new DurableTaskOptions();
            options.StorageProvider["type"] = storageProvider;

            using (ITestHost host = TestHelpers.GetJobHostWithMultipleDurabilityProviders(
                options: options,
                durabilityProviderFactories: durabilityProviderFactories))
            {
                azureStorageMock.Verify(a => a.GetDurabilityProvider(), Times.Never());
                netheriteMock.Verify(n => n.GetDurabilityProvider(), Times.Once());
                microsoftSQLMock.Verify(m => m.GetDurabilityProvider(), Times.Never());
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("MicrosoftSQL")]
        public void SelectingMicrosoftSQLStorageProvider(string storageProvider)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            Mock<IDurabilityProviderFactory> azureStorageMock = GetAzureStorageStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> microsoftSQLMock = GetMicrosoftSQLStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> netheriteMock = GetNetheriteStorageProviderMock(orchestrationServiceClientMock);

            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = new[] { azureStorageMock.Object, microsoftSQLMock.Object, netheriteMock.Object };

            DurableTaskOptions options = new DurableTaskOptions();
            options.StorageProvider["type"] = storageProvider;

            using (ITestHost host = TestHelpers.GetJobHostWithMultipleDurabilityProviders(
                options: options,
                durabilityProviderFactories: durabilityProviderFactories))
            {
                azureStorageMock.Verify(a => a.GetDurabilityProvider(), Times.Never());
                netheriteMock.Verify(n => n.GetDurabilityProvider(), Times.Never());
                microsoftSQLMock.Verify(m => m.GetDurabilityProvider(), Times.Once());
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void SelectingFromEmptyFactoryListThrowsException()
        {
            IEnumerable<IDurabilityProviderFactory> emptyDurabilityProviderFactoriesList = Enumerable.Empty<IDurabilityProviderFactory>();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.GetJobHostWithMultipleDurabilityProviders(
                    durabilityProviderFactories: emptyDurabilityProviderFactoriesList));

            Assert.Equal($"Couldn't find the default storage provider: AzureStorage.", ex.Message);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("storage")]
        public void SelectingUnavailableStorageProviderThrowsException(string storageProvider)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            Mock<IDurabilityProviderFactory> azureStorageMock = GetAzureStorageStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> microsoftSQLMock = GetMicrosoftSQLStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> netheriteMock = GetNetheriteStorageProviderMock(orchestrationServiceClientMock);

            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = new[] { azureStorageMock.Object, microsoftSQLMock.Object, netheriteMock.Object };

            DurableTaskOptions options = new DurableTaskOptions();
            options.StorageProvider["type"] = storageProvider;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.GetJobHostWithMultipleDurabilityProviders(
                    options: options,
                    durabilityProviderFactories: durabilityProviderFactories));

            IList<string> factoryNames = durabilityProviderFactories.Select(f => f.Name).ToList();

            Assert.Equal($"Storage provider type ({storageProvider}) was not found. Available storage providers: {string.Join(", ", factoryNames)}.", ex.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void SelectingDefaultStorageProviderWhenNoTypeIsProvided()
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            Mock<IDurabilityProviderFactory> azureStorageMock = GetAzureStorageStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> microsoftSQLMock = GetMicrosoftSQLStorageProviderMock(orchestrationServiceClientMock);
            Mock<IDurabilityProviderFactory> netheriteMock = GetNetheriteStorageProviderMock(orchestrationServiceClientMock);

            IEnumerable<IDurabilityProviderFactory> durabilityProviderFactories = new[] { azureStorageMock.Object, microsoftSQLMock.Object, netheriteMock.Object };

            using (ITestHost host = TestHelpers.GetJobHostWithMultipleDurabilityProviders(
                durabilityProviderFactories: durabilityProviderFactories))
            {
                netheriteMock.Verify(n => n.GetDurabilityProvider(), Times.Never());
                azureStorageMock.Verify(a => a.GetDurabilityProvider(), Times.Once());
                microsoftSQLMock.Verify(m => m.GetDurabilityProvider(), Times.Never());
            }
        }

        private static Mock<IDurabilityProviderFactory> GetAzureStorageStorageProviderMock(Mock<IOrchestrationServiceClient> orchestrationServiceClientMock)
        {
            Mock<IDurabilityProviderFactory> azureStorageMock = new Mock<IDurabilityProviderFactory>();
            azureStorageMock.Setup(f => f.Name).Returns("AzureStorage");
            azureStorageMock.Setup(f => f.GetDurabilityProvider()).Returns(new DurabilityProvider("AzureStorage", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test"));

            return azureStorageMock;
        }

        private static Mock<IDurabilityProviderFactory> GetMicrosoftSQLStorageProviderMock(Mock<IOrchestrationServiceClient> orchestrationServiceClientMock)
        {
            Mock<IDurabilityProviderFactory> microsoftSQLMock = new Mock<IDurabilityProviderFactory>();
            microsoftSQLMock.Setup(f => f.Name).Returns("MicrosoftSQL");
            microsoftSQLMock.Setup(f => f.GetDurabilityProvider()).Returns(new DurabilityProvider("MicrosoftSQL", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test"));
            return microsoftSQLMock;
        }

        private static Mock<IDurabilityProviderFactory> GetNetheriteStorageProviderMock(Mock<IOrchestrationServiceClient> orchestrationServiceClientMock)
        {
            Mock<IDurabilityProviderFactory> netheriteMock = new Mock<IDurabilityProviderFactory>();
            netheriteMock.Setup(f => f.Name).Returns("Netherite");
            netheriteMock.Setup(f => f.GetDurabilityProvider()).Returns(new DurabilityProvider("Netherite", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test"));
            return netheriteMock;
        }
    }
}
