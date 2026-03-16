// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class StorageServiceClientProviderFactoryTests
    {
        private const string EmulatorKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        private readonly AzureComponentFactory componentFactory;
        private readonly AzureEventSourceLogForwarder logForwarder;

        public StorageServiceClientProviderFactoryTests()
        {
            ServiceCollection services = new ServiceCollection();

            services
                .AddLogging(c => c.ClearProviders().AddProvider(NullLoggerProvider.Instance))
                .AddAzureClientsCore();

            IServiceProvider provider = services.BuildServiceProvider();
            this.componentFactory = provider.GetRequiredService<AzureComponentFactory>();
            this.logForwarder = provider.GetRequiredService<AzureEventSourceLogForwarder>();
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetBlobClientProvider_MissingConfig()
        {
            const string connectionName = "storage";
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupClientProviderFactory(connectionName, connectionString: null);
            Assert.Throws<InvalidOperationException>(() => clientProviderFactory.GetBlobClientProvider(connectionName));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetQueueClientProvider_MissingConfig()
        {
            const string connectionName = "storage";
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupClientProviderFactory(connectionName, connectionString: null);
            Assert.Throws<InvalidOperationException>(() => clientProviderFactory.GetQueueClientProvider(connectionName));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetTableClientProvider_MissingConfig()
        {
            const string connectionName = "storage";
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupClientProviderFactory(connectionName, connectionString: null);
            Assert.Throws<InvalidOperationException>(() => clientProviderFactory.GetTableClientProvider(connectionName));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("UseDevelopmentStorage=true", "http://127.0.0.1:10000/devstoreaccount1")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest1;AccountKey={EmulatorKey};BlobEndpoint=https://unittest1.blob.core.windows.net;", "https://unittest1.blob.core.windows.net")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest2;AccountKey={EmulatorKey};BlobEndpoint=https://unittest2.blob.core.usgovcloudapi.net;", "https://unittest2.blob.core.usgovcloudapi.net")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest3;AccountKey={EmulatorKey};BlobEndpoint=https://unittest3.blob.core.foo.bar.baz;", "https://unittest3.blob.core.foo.bar.baz")]
        public void GetBlobClientProvider_ConnectionString(string connectionString, string expectedEndpoint)
        {
            const string connectionName = "storage";
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupClientProviderFactory(connectionName, connectionString);

            IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> provider = clientProviderFactory.GetBlobClientProvider(connectionName);
            BlobServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(new Uri(expectedEndpoint, UriKind.Absolute), actual.Uri);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("UseDevelopmentStorage=true", "http://127.0.0.1:10001/devstoreaccount1")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest1;AccountKey={EmulatorKey};QueueEndpoint=https://unittest1.queue.core.windows.net;", "https://unittest1.queue.core.windows.net")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest2;AccountKey={EmulatorKey};QueueEndpoint=https://unittest2.queue.core.usgovcloudapi.net;", "https://unittest2.queue.core.usgovcloudapi.net")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest3;AccountKey={EmulatorKey};QueueEndpoint=https://unittest3.queue.core.foo.bar.baz;", "https://unittest3.queue.core.foo.bar.baz")]
        public void GetQueueClientProvider_ConnectionString(string connectionString, string expectedEndpoint)
        {
            const string connectionName = "storage";
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupClientProviderFactory(connectionName, connectionString);

            IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> provider = clientProviderFactory.GetQueueClientProvider(connectionName);
            QueueServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(new Uri(expectedEndpoint, UriKind.Absolute), actual.Uri);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("UseDevelopmentStorage=true", "http://127.0.0.1:10002/devstoreaccount1")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest1;AccountKey={EmulatorKey};TableEndpoint=https://unittest1.table.core.windows.net;", "https://unittest1.table.core.windows.net")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest2;AccountKey={EmulatorKey};TableEndpoint=https://unittest2.table.core.usgovcloudapi.net;", "https://unittest2.table.core.usgovcloudapi.net")]
        [InlineData($"DefaultEndpointsProtocol=https;AccountName=unittest3;AccountKey={EmulatorKey};TableEndpoint=https://unittest3.table.core.foo.bar.baz;", "https://unittest3.table.core.foo.bar.baz")]
        public void GetTableClientProvider_ConnectionString(string connectionString, string expectedEndpoint)
        {
            const string connectionName = "storage";
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupClientProviderFactory(connectionName, connectionString);

            IStorageServiceClientProvider<TableServiceClient, TableClientOptions> provider = clientProviderFactory.GetTableClientProvider(connectionName);
            TableServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(new Uri(expectedEndpoint, UriKind.Absolute), actual.Uri);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("unittest1", "https://unittest1.blob.core.windows.net")]
        [InlineData("unittest2", "https://unittest2.blob.core.usgovcloudapi.net")]
        [InlineData("unittest3", "https://unittest3.blob.core.foo.bar.baz")]
        public void GetBlobClientProvider_ConfigSection_Endpoint(string accountName, string endpoint)
        {
            const string connectionName = "storage";
            var endpointUri = new Uri(endpoint, UriKind.Absolute);
            var options = new AzureStorageAccountOptions { BlobServiceUri = endpointUri };
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> provider = clientProviderFactory.GetBlobClientProvider(connectionName);
            BlobServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(endpointUri, actual.Uri);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("unittest1", "https://unittest1.queue.core.windows.net")]
        [InlineData("unittest2", "https://unittest2.queue.core.usgovcloudapi.net")]
        [InlineData("unittest3", "https://unittest3.queue.core.foo.bar.baz")]
        public void GetQueueClientProvider_ConfigSection_Endpoint(string accountName, string endpoint)
        {
            const string connectionName = "storage";
            var endpointUri = new Uri(endpoint, UriKind.Absolute);
            var options = new AzureStorageAccountOptions { QueueServiceUri = endpointUri };
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> provider = clientProviderFactory.GetQueueClientProvider(connectionName);
            QueueServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(endpointUri, actual.Uri);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("unittest1", "https://unittest1.table.core.windows.net")]
        [InlineData("unittest2", "https://unittest2.table.core.usgovcloudapi.net")]
        [InlineData("unittest3", "https://unittest3.table.core.foo.bar.baz")]
        public void GetTableClientProvider_ConfigSection_Endpoint(string accountName, string endpoint)
        {
            const string connectionName = "storage";
            var endpointUri = new Uri(endpoint, UriKind.Absolute);
            var options = new AzureStorageAccountOptions { TableServiceUri = endpointUri };
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<TableServiceClient, TableClientOptions> provider = clientProviderFactory.GetTableClientProvider(connectionName);
            TableServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(endpointUri, actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetBlobClientProvider_ConfigSection_Account()
        {
            const string accountName = "someaccount";
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = accountName };
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> provider = clientProviderFactory.GetBlobClientProvider(connectionName);
            BlobServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(new Uri($"https://{accountName}.blob.core.windows.net", UriKind.Absolute), actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetQueueClientProvider_ConfigSection_Account()
        {
            const string accountName = "someaccount";
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = accountName };
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> provider = clientProviderFactory.GetQueueClientProvider(connectionName);
            QueueServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(new Uri($"https://{accountName}.queue.core.windows.net", UriKind.Absolute), actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetTableClientProvider_ConfigSection_Account()
        {
            const string accountName = "someaccount";
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = accountName };
            StorageServiceClientProviderFactory clientProviderFactory = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<TableServiceClient, TableClientOptions> provider = clientProviderFactory.GetTableClientProvider(connectionName);
            TableServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(new Uri($"https://{accountName}.table.core.windows.net", UriKind.Absolute), actual.Uri);
        }

        private StorageServiceClientProviderFactory SetupStorageAccountExplorer(string connectionName, AzureStorageAccountOptions options)
        {
            IConfigurationSection config = new ConfigurationBuilder()
                .AddInMemoryCollection(Serialize(options).Select(x => new KeyValuePair<string, string>(connectionName + ':' + x.Key, x.Value)))
                .Build()
                .GetSection(connectionName);

            var mockResolver = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mockResolver.Setup(r => r.Resolve(connectionName)).Returns(config);

            return new StorageServiceClientProviderFactory(mockResolver.Object, this.componentFactory, this.logForwarder);
        }

        private static Dictionary<string, string> Serialize<T>(T value)
        {
            // Quick and dirty way of serializing all of these values to the config
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            settings.Converters.Add(new StringEnumConverter());

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(
                JsonConvert.SerializeObject(value, settings),
                settings);
        }

        private StorageServiceClientProviderFactory SetupClientProviderFactory(string connectionName, string connectionString)
        {
            var mock = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mock.Setup(r => r.Resolve(connectionName)).Returns(new ReadOnlyConfigurationValue(connectionName, connectionString));

            return new StorageServiceClientProviderFactory(mock.Object, this.componentFactory, this.logForwarder);
        }

        private sealed class AzureStorageAccountOptions
        {
            public string AccountName { get; set; }

            public Uri BlobServiceUri { get; set; }

            public Uri QueueServiceUri { get; set; }

            public Uri TableServiceUri { get; set; }
        }
    }
}