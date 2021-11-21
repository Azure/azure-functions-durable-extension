// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class AzureStorageAccountProviderTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetStorageAccountDetails_ConnectionString()
        {
            const string connectionName = "storage";
            const string connectionString = "MyConnectionString";
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, connectionString);

            StorageAccountDetails actual = provider.GetStorageAccountDetails(connectionName);
            Assert.Equal(connectionString, actual.ConnectionString);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetStorageAccountDetails_Endpoints()
        {
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions
            {
                QueueServiceUri = new Uri("https://unit-test/queue", UriKind.Absolute),
                TableServiceUri = new Uri("https://unit-test/table", UriKind.Absolute),
            };
            var credentials = new StorageCredentials();
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, options, credentials);

            StorageAccountDetails actual = provider.GetStorageAccountDetails(connectionName);
            Assert.Null(actual.ConnectionString);
            Assert.Same(credentials, actual.StorageCredentials);

            // TODO: Add properties to durable task
            // Assert.Equal(options.QueueServiceUri, actual);
            // Assert.Equal(options.TableServiceUri, actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetStorageAccountDetails_DefaultEndpoints()
        {
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions
            {
                QueueServiceUri = new Uri("https://unit-test/queue", UriKind.Absolute),
            };
            var credentials = new StorageCredentials();
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, options, credentials);

            StorageAccountDetails actual = provider.GetStorageAccountDetails(connectionName);
            Assert.Null(actual.ConnectionString);
            Assert.Same(credentials, actual.StorageCredentials);

            // TODO: Add properties to durable task
            // Assert.Equal(options.QueueServiceUri, actual);
            // Assert.Equal(options.TableServiceUri, actual);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetStorageAccountDetails_AccountName()
        {
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = "MyAccount" };
            var credentials = new StorageCredentials();
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, options, credentials);

            StorageAccountDetails actual = provider.GetStorageAccountDetails(connectionName);
            Assert.Null(actual.ConnectionString);
            Assert.Equal(options.AccountName, actual.AccountName);
            Assert.Equal(AzureStorageAccountOptions.DefaultEndpointSuffix, actual.EndpointSuffix);
            Assert.Equal(credentials, actual.StorageCredentials);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetCloudStorageAccount_ConnectionString()
        {
            const string connectionName = "storage";
            const string connectionString = "UseDevelopmentStorage=true";
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, connectionString);

            CloudStorageAccount actual = provider.GetCloudStorageAccount(connectionName);
            Assert.Equal(new Uri("http://127.0.0.1:10000/devstoreaccount1", UriKind.Absolute), actual.BlobEndpoint);
            Assert.Equal(new Uri("http://127.0.0.1:10001/devstoreaccount1", UriKind.Absolute), actual.QueueEndpoint);
            Assert.Equal(new Uri("http://127.0.0.1:10002/devstoreaccount1", UriKind.Absolute), actual.TableEndpoint);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetCloudStorageAccount_Endpoints()
        {
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions
            {
                QueueServiceUri = new Uri("https://unit-test/queue", UriKind.Absolute),
                TableServiceUri = new Uri("https://unit-test/table", UriKind.Absolute),
            };
            var credentials = new StorageCredentials();
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, options, credentials);

            CloudStorageAccount actual = provider.GetCloudStorageAccount(connectionName);
            Assert.Same(credentials, actual.Credentials);
            Assert.Equal(options.QueueServiceUri, actual.QueueEndpoint);
            Assert.Equal(options.TableServiceUri, actual.TableEndpoint);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetCloudStorageAccount_DefaultEndpoints()
        {
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions
            {
                AccountName = "unit-test",
                QueueServiceUri = new Uri("https://unit-test/queue", UriKind.Absolute),
            };
            var credentials = new StorageCredentials();
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, options, credentials);

            CloudStorageAccount actual = provider.GetCloudStorageAccount(connectionName);
            Assert.Same(credentials, actual.Credentials);
            Assert.Equal(options.QueueServiceUri, actual.QueueEndpoint);
            Assert.Equal(options.GetDefaultServiceUri("table"), actual.TableEndpoint);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetCloudStorageAccount_AccountName()
        {
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = "MyAccount" };
            var credentials = new StorageCredentials();
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, options, credentials);

            CloudStorageAccount actual = provider.GetCloudStorageAccount(connectionName);
            Assert.Equal(options.GetDefaultServiceUri("queue"), actual.QueueEndpoint);
            Assert.Equal(options.GetDefaultServiceUri("table"), actual.TableEndpoint);
        }

        private static AzureStorageAccountProvider SetupStorageAccountProvider(string connectionName, string connectionString)
        {
            var mock = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mock.Setup(r => r.Resolve(connectionName)).Returns(connectionString);

            return new AzureStorageAccountProvider(mock.Object);
        }

        private static AzureStorageAccountProvider SetupStorageAccountProvider(string connectionName, AzureStorageAccountOptions options, StorageCredentials credentials)
        {
            var mockResolver = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mockResolver.Setup(r => r.Resolve(connectionName)).Returns<string>(null);
            mockResolver.Setup(r => r.Resolve<AzureStorageAccountOptions>(connectionName)).Returns(options);

            var mockFactory = new Mock<IStorageCredentialsFactory>(MockBehavior.Strict);
            mockFactory.Setup(f => f.CreateAsync(options, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);

            return new AzureStorageAccountProvider(mockResolver.Object, mockFactory.Object);
        }
    }
}