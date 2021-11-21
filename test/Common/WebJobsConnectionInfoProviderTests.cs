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
    public class WebJobsConnectionInfoProviderTests
    {
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public void ResolveConnectionString(bool prefix)
        {
#if FUNCTIONS_V1
            // Instead of mocking ConfigurationManager.ConnectionStrings, use environment variables
            string connectionName = Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(Prefix(connectionName, prefix), "Foo=Bar;Baz", EnvironmentVariableTarget.Process);

            try
            {
                var provider = new WebJobsConnectionInfoProvider();
                Assert.Equal("Foo=Bar;Baz", provider.Resolve(connectionName));
                Assert.Null(provider.Resolve(Guid.NewGuid().ToString()));
            }
            finally
            {
                Environment.SetEnvironmentVariable(Prefix(connectionName, prefix), null, EnvironmentVariableTarget.Process);
            }
#else
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>(Prefix("MyConnection", prefix), "Foo=Bar;Baz"),
                    new KeyValuePair<string, string>($"ConnectionStrings:{Prefix("MyOtherConnection", prefix)}", "https://foo.bar/baz"),
                })
                .Build();

            var provider = new WebJobsConnectionInfoProvider(config);

            Assert.Equal("Foo=Bar;Baz", provider.Resolve("MyConnection"));
            Assert.Equal("https://foo.bar/baz", provider.Resolve("MyOtherConnection"));
#endif
        }

#if FUNCTIONS_V1
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Resolve()
        {
            Assert.Throws<NotSupportedException>(() => new WebJobsConnectionInfoProvider().Resolve<AzureStorageAccountOptions>("MyConnection"));
        }
#else
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public void Resolve(bool prefix)
        {
            const string connectionName = "MyConnection";
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new KeyValuePair<string, string>[]
                {
                    // Users will not specify every value in the AzureStorageAccountOptions,
                    // but this test demonstrates that any of these properties can be populated via a configuration
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.AccountName)}", "MyAccount"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.BlobServiceUri)}", "https://unit-test/blob"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.Certificate)}", "12345ABCDE"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.ClientCertificateStoreLocation)}", nameof(StoreLocation.LocalMachine)),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.ClientId)}", "MyClient"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.ClientSecret)}", "Shhh!"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.ConnectionString)}", "Foo=Bar;Baz"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.Credential)}", "Everything"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.QueueServiceUri)}", "https://unit-test/queue"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.TableServiceUri)}", "https://unit-test/table"),
                    new KeyValuePair<string, string>($"{Prefix(connectionName, prefix)}:{nameof(AzureStorageAccountOptions.TenantId)}", "MyTenant"),
                })
                .Build();

            var provider = new WebJobsConnectionInfoProvider(config);

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
        }
#endif

        private static string Prefix(string name, bool prefix) =>
            prefix ? "AzureWebJobs" + name : name;
    }
}