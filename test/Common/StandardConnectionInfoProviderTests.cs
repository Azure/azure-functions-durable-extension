// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class StandardConnectionInfoProviderTests
    {
        [Fact]
        public void ResolveConnectionString()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("MyConnection", "Foo=Bar;Baz"),
                })
                .Build();

            var provider = new StandardConnectionInfoProvider(config);

            Assert.Equal("Foo=Bar;Baz", provider.Resolve("MyConnection"));
            Assert.Null(provider.Resolve("MyOtherConnection"));
        }

        [Fact]
        public void Resolve()
        {
            const string connectionName = "MyConnection";
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new KeyValuePair<string, string>[]
                {
                    // Users will not specify every value in the AzureStorageAccountOptions,
                    // but this test demonstrates that any of these properties can be populated via a configuration
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.AccountName)}", "MyAccount"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.BlobServiceUri)}", "https://unit-test/blob"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.Certificate)}", "12345ABCDE"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.ClientCertificateStoreLocation)}", nameof(StoreLocation.LocalMachine)),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.ClientId)}", "MyClient"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.ClientSecret)}", "Shhh!"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.ConnectionString)}", "Foo=Bar;Baz"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.Credential)}", "Everything"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.QueueServiceUri)}", "https://unit-test/queue"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.TableServiceUri)}", "https://unit-test/table"),
                    new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.TenantId)}", "MyTenant"),
                })
                .Build();

            var provider = new StandardConnectionInfoProvider(config);

#if FUNCTIONS_V1
            Assert.Throws<NotSupportedException>(() => provider.Resolve<AzureStorageAccountOptions>(connectionName));
#else
            AzureStorageAccountOptions actual = provider.Resolve<AzureStorageAccountOptions>(connectionName);
            Assert.Equal("MyAccount", actual.AccountName);
            Assert.Equal(new Uri("https://unit-test/blob", UriKind.Absolute), actual.BlobServiceUri);
            Assert.Equal("12345ABCDE", actual.Certificate);
            Assert.Equal(StoreLocation.LocalMachine, actual.ClientCertificateStoreLocation);
            Assert.Equal("MyClient", actual.ClientId);
            Assert.Equal("Shhh!", actual.ClientSecret);
            Assert.Equal("Foo=Bar;Baz", actual.ConnectionString);
            Assert.Equal("Everything", actual.Credential);
            Assert.Equal(new Uri("https://unit-test/queue", UriKind.Absolute), actual.QueueServiceUri);
            Assert.Equal(new Uri("https://unit-test/table", UriKind.Absolute), actual.TableServiceUri);
            Assert.Equal("MyTenant", actual.TenantId);
#endif
        }
    }
}