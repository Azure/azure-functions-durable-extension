// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
#if !FUNCTIONS_V1
using System.Collections.Generic;
using System.Linq;
#endif
using System.Linq.Expressions;
using System.Reflection;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Storage;
#if !FUNCTIONS_V1
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
#endif
using Moq;
#if !FUNCTIONS_V1
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
#endif
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class AzureStorageAccountExplorerTests
    {
        // TODO: Replace this with the Uri property when the Table package can be updated
        private static readonly Func<TableServiceClient, Uri> GetEndpointFunc = CreateGetEndpointFunc();

#if !FUNCTIONS_V1
        private readonly AzureComponentFactory componentFactory;
        private readonly AzureEventSourceLogForwarder logForwarder;

        public AzureStorageAccountExplorerTests()
        {
            ServiceCollection services = new ServiceCollection();

            services
                .AddLogging(c => c.ClearProviders().AddProvider(NullLoggerProvider.Instance))
                .AddAzureClientsCore();

            IServiceProvider provider = services.BuildServiceProvider();
            this.componentFactory = provider.GetRequiredService<AzureComponentFactory>();
            this.logForwarder = provider.GetRequiredService<AzureEventSourceLogForwarder>();
        }
#endif

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetBlobClientProvider_MissingConfig()
        {
            const string connectionName = "storage";
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, connectionString: null);
            Assert.Throws<InvalidOperationException>(() => explorer.GetBlobClientProvider(connectionName));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetQueueClientProvider_MissingConfig()
        {
            const string connectionName = "storage";
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, connectionString: null);
            Assert.Throws<InvalidOperationException>(() => explorer.GetQueueClientProvider(connectionName));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetTableClientProvider_MissingConfig()
        {
            const string connectionName = "storage";
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, connectionString: null);
            Assert.Throws<InvalidOperationException>(() => explorer.GetTableClientProvider(connectionName));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetBlobClientProvider_ConnectionString()
        {
            const string connectionName = "storage";
            const string connectionString = "UseDevelopmentStorage=true";
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, connectionString);

            IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> provider = explorer.GetBlobClientProvider(connectionName);
            BlobServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(new Uri("http://127.0.0.1:10000/devstoreaccount1", UriKind.Absolute), actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetQueueClientProvider_ConnectionString()
        {
            const string connectionName = "storage";
            const string connectionString = "UseDevelopmentStorage=true";
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, connectionString);

            IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> provider = explorer.GetQueueClientProvider(connectionName);
            QueueServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(new Uri("http://127.0.0.1:10001/devstoreaccount1", UriKind.Absolute), actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetTableClientProvider_ConnectionString()
        {
            const string connectionName = "storage";
            const string connectionString = "UseDevelopmentStorage=true";
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, connectionString);

            IStorageServiceClientProvider<TableServiceClient, TableClientOptions> provider = explorer.GetTableClientProvider(connectionName);
            TableServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(new Uri("http://127.0.0.1:10002/devstoreaccount1", UriKind.Absolute), GetEndpointFunc(actual));
        }

#if !FUNCTIONS_V1

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetBlobClientProvider_ConfigSection_Endpoint()
        {
            const string accountName = "unit-test";
            const string connectionName = "storage";
            var endpoint = new Uri($"https://{accountName}.blob.core.windows.net", UriKind.Absolute);
            var options = new AzureStorageAccountOptions { BlobServiceUri = endpoint };
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> provider = explorer.GetBlobClientProvider(connectionName);
            BlobServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(endpoint, actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetQueueClientProvider_ConfigSection_Endpoint()
        {
            const string accountName = "unit-test";
            const string connectionName = "storage";
            var endpoint = new Uri($"https://{accountName}.queue.core.windows.net", UriKind.Absolute);
            var options = new AzureStorageAccountOptions { QueueServiceUri = endpoint };
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> provider = explorer.GetQueueClientProvider(connectionName);
            QueueServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(endpoint, actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetTableClientProvider_ConfigSection_Endpoint()
        {
            const string accountName = "unit-test";
            const string connectionName = "storage";
            var endpoint = new Uri($"https://{accountName}.table.core.windows.net", UriKind.Absolute);
            var options = new AzureStorageAccountOptions { TableServiceUri = endpoint };
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<TableServiceClient, TableClientOptions> provider = explorer.GetTableClientProvider(connectionName);
            TableServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(endpoint, GetEndpointFunc(actual));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetBlobClientProvider_ConfigSection_Account()
        {
            const string accountName = "some-account";
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = accountName };
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<BlobServiceClient, BlobClientOptions> provider = explorer.GetBlobClientProvider(connectionName);
            BlobServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(new Uri($"https://{accountName}.blob.core.windows.net", UriKind.Absolute), actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetQueueClientProvider_ConfigSection_Account()
        {
            const string accountName = "some-account";
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = accountName };
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<QueueServiceClient, QueueClientOptions> provider = explorer.GetQueueClientProvider(connectionName);
            QueueServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(new Uri($"https://{accountName}.queue.core.windows.net", UriKind.Absolute), actual.Uri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetTableClientProvider_ConfigSection_Account()
        {
            const string accountName = "some-account";
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions { AccountName = accountName };
            AzureStorageAccountExplorer explorer = this.SetupStorageAccountExplorer(connectionName, options);

            IStorageServiceClientProvider<TableServiceClient, TableClientOptions> provider = explorer.GetTableClientProvider(connectionName);
            TableServiceClient actual = provider.CreateClient(provider.CreateOptions());
            Assert.Equal(accountName, actual.AccountName);
            Assert.Equal(new Uri($"https://{accountName}.table.core.windows.net", UriKind.Absolute), GetEndpointFunc(actual));
        }

        private AzureStorageAccountExplorer SetupStorageAccountExplorer(string connectionName, AzureStorageAccountOptions options)
        {
            IConfigurationSection config = new ConfigurationBuilder()
                .AddInMemoryCollection(Serialize(options).Select(x => new KeyValuePair<string, string>(connectionName + ':' + x.Key, x.Value)))
                .Build()
                .GetSection(connectionName);

            var mockResolver = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mockResolver.Setup(r => r.Resolve(connectionName)).Returns(config);

            return new AzureStorageAccountExplorer(mockResolver.Object, this.componentFactory, this.logForwarder);
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

#endif

        private AzureStorageAccountExplorer SetupStorageAccountExplorer(string connectionName, string connectionString)
        {
            var mock = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mock.Setup(r => r.Resolve(connectionName)).Returns(new ReadOnlyConfigurationValue(connectionName, connectionString));

#if FUNCTIONS_V1
            return new AzureStorageAccountExplorer(mock.Object);
#else
            return new AzureStorageAccountExplorer(mock.Object, this.componentFactory, this.logForwarder);
#endif
        }

        private static Func<TableServiceClient, Uri> CreateGetEndpointFunc()
        {
            Type tableClientType = typeof(TableServiceClient);
            ParameterExpression clientParam = Expression.Parameter(typeof(TableServiceClient), "client");
            FieldInfo endpointField = tableClientType.GetField("_endpoint", BindingFlags.Instance | BindingFlags.NonPublic);

            return Expression
                .Lambda<Func<TableServiceClient, Uri>>(Expression.Field(clientParam, endpointField), clientParam)
                .Compile();
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