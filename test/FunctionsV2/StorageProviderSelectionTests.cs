// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class StorageProviderSelectionTests
    {
        static StorageProviderSelectionTests()
        {
            AzureStorageDurabilityProviderFactoryMock = new Mock<IDurabilityProviderFactory>();
            AzureStorageDurabilityProviderFactoryMock.Setup(f => f.Name).Returns("AzureStorage");

            MicrosoftSQLDurabilityProviderFactoryMock = new Mock<IDurabilityProviderFactory>();
            MicrosoftSQLDurabilityProviderFactoryMock.Setup(f => f.Name).Returns("MicrosoftSQL");

            NetheriteProviderDurabilityProviderFactoryMock = new Mock<IDurabilityProviderFactory>();
            NetheriteProviderDurabilityProviderFactoryMock.Setup(f => f.Name).Returns("Netherite");

            AllDurabilityProviderFactories = new[] { AzureStorageDurabilityProviderFactoryMock.Object, MicrosoftSQLDurabilityProviderFactoryMock.Object, NetheriteProviderDurabilityProviderFactoryMock.Object };
            EmptyDurabilityProviderFactoriesList = Enumerable.Empty<IDurabilityProviderFactory>();

            ValidStorageProviderTypesData = new List<object[]>
            {
                new object[] { "AzureStorage", AzureStorageDurabilityProviderFactoryMock.Object },
                new object[] { "MicrosoftSQL", MicrosoftSQLDurabilityProviderFactoryMock.Object },
                new object[] { "Netherite", NetheriteProviderDurabilityProviderFactoryMock.Object },
            };
        }

        public StorageProviderSelectionTests() { }

        private static Mock<IDurabilityProviderFactory> AzureStorageDurabilityProviderFactoryMock { get; }

        private static Mock<IDurabilityProviderFactory> MicrosoftSQLDurabilityProviderFactoryMock { get; }

        private static Mock<IDurabilityProviderFactory> NetheriteProviderDurabilityProviderFactoryMock { get; }

        private static IEnumerable<IDurabilityProviderFactory> AllDurabilityProviderFactories { get; }

        private static IEnumerable<IDurabilityProviderFactory> EmptyDurabilityProviderFactoriesList { get; }

        public static IEnumerable<object[]> ValidStorageProviderTypesData { get; }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(ValidStorageProviderTypesData))]
        public void SelectingAvailableStorageProvider(string storageProvider, IDurabilityProviderFactory expectedFactory)
        {
            IDurabilityProviderFactory defaultFactory = AllDurabilityProviderFactories.First(f => f.Name.Equals(storageProvider));

            Assert.Equal(expectedFactory, defaultFactory);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("AzureStorage")]
        public void SelectingFromEmptyFactoryListThrowsException(string storageProvider)
        {
            Assert.Throws<InvalidOperationException>(() => EmptyDurabilityProviderFactoriesList.First(f => f.Name.Equals(storageProvider)));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("storage")]
        public void SelectingUnavailableStorageProviderThrowsException(string storageProvider)
        {
            Assert.Throws<InvalidOperationException>(() => AllDurabilityProviderFactories.First(f => f.Name.Equals(storageProvider)));
        }
    }
}
