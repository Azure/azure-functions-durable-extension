// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DurableTask.AzureStorage;
#if !FUNCTIONS_V1
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Auth;
#endif
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Auth;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class AzureStorageAccountProviderTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetStorageAccountDetails_MissingConfig()
        {
            const string connectionName = "storage";
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, connectionString: null);
            Assert.Throws<InvalidOperationException>(() => provider.GetStorageAccountDetails(connectionName));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetStorageAccountDetails_ConnectionString()
        {
            const string connectionName = "storage";
            const string connectionString = "MyConnectionString";
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, connectionString);

            StorageAccountDetails actual = provider.GetStorageAccountDetails(connectionName);
            Assert.Null(actual.AccountName);
            Assert.Null(actual.BlobServiceUri);
            Assert.Equal(connectionString, actual.ConnectionString);
            Assert.Null(actual.EndpointSuffix);
            Assert.Null(actual.QueueServiceUri);
            Assert.Null(actual.StorageCredentials);
            Assert.Null(actual.TableServiceUri);
        }

#if !FUNCTIONS_V1

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetStorageAccountDetails_ConfigSection()
        {
            const string connectionName = "storage";
            var options = new AzureStorageAccountOptions
            {
                AccountName = "MyAccountName",
                BlobServiceUri = new Uri("https://unit-test/blob", UriKind.Absolute),
                QueueServiceUri = new Uri("https://unit-test/queue", UriKind.Absolute),
                TableServiceUri = new Uri("https://unit-test/table", UriKind.Absolute),
            };
            AzureStorageAccountProvider provider = SetupStorageAccountProvider(connectionName, options);

            StorageAccountDetails actual = provider.GetStorageAccountDetails(connectionName);
            Assert.Equal(options.AccountName, actual.AccountName);
            Assert.Equal(options.BlobServiceUri, actual.BlobServiceUri);
            Assert.Null(actual.ConnectionString);
            Assert.Equal(AzureStorageAccountOptions.DefaultEndpointSuffix, actual.EndpointSuffix);
            Assert.Equal(options.QueueServiceUri, actual.QueueServiceUri);
            Assert.True(actual.StorageCredentials.IsToken);
            Assert.Equal(options.TableServiceUri, actual.TableServiceUri);
        }

        private static AzureStorageAccountProvider SetupStorageAccountProvider(string connectionName, AzureStorageAccountOptions options)
        {
            var credential = new TokenCredential("AAAA");
            IConfigurationSection config = new ConfigurationBuilder()
                .AddInMemoryCollection(Serialize(options).Select(x => new KeyValuePair<string, string>(connectionName + ':' + x.Key, x.Value)))
                .Build()
                .GetSection(connectionName);

            var mockResolver = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mockResolver.Setup(r => r.Resolve(connectionName)).Returns(config);

            var mockFactory = new Mock<ITokenCredentialFactory>(MockBehavior.Strict);
            mockFactory.Setup(f => f
                .Create(config, It.IsAny<CancellationToken>()))
                .Returns(credential);

            return new AzureStorageAccountProvider(mockResolver.Object, mockFactory.Object);
        }

        private static Dictionary<string, string> Serialize(AzureStorageAccountOptions value)
        {
            // Quick and dirty way of serializing all of these values to the config
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            settings.Converters.Add(new StringEnumConverter());

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(
                JsonConvert.SerializeObject(value, settings),
                settings);
        }

#endif

        private static AzureStorageAccountProvider SetupStorageAccountProvider(string connectionName, string connectionString)
        {
            var mock = new Mock<IConnectionInfoResolver>(MockBehavior.Strict);
            mock.Setup(r => r.Resolve(connectionName)).Returns(new ReadOnlyConfigurationValue(connectionName, connectionString));

#if FUNCTIONS_V1
            return new AzureStorageAccountProvider(mock.Object);
#else
            return new AzureStorageAccountProvider(mock.Object, new Mock<ITokenCredentialFactory>(MockBehavior.Strict).Object);
#endif
        }
    }
}