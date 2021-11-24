// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class AzureStorageAccountOptionsTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetDefaultServiceUriWithNoAccount()
        {
            Assert.Throws<InvalidOperationException>(() => new AzureStorageAccountOptions().GetDefaultServiceUri("blob"));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("foo", "blob", "https://foo.blob.core.windows.net/")]
        [InlineData("bar", "file", "https://bar.file.core.windows.net/")]
        [InlineData("baz", "queue", "https://baz.queue.core.windows.net/")]
        [InlineData("foobar", "table", "https://foobar.table.core.windows.net/")]
        public void GetDefaultServiceUri(string accountName, string service, string expected)
        {
            var options = new AzureStorageAccountOptions { AccountName = accountName };
            Assert.Equal(new Uri(expected, UriKind.Absolute), options.GetDefaultServiceUri(service));
        }

#if !FUNCTIONS_V1
        [Fact]
        public void GetFromConfiguration()
        {
            const string connectionName = "MyConnection";
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new KeyValuePair<string, string>[]
                    {
                        // Users will not specify every value in the AzureStorageAccountOptions,
                        // but this test demonstrates that any of these properties can be populated via a configuration
                        new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.AccountName)}", "MyAccount"),
                        new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.BlobServiceUri)}", "https://unit-test/blob"),
                        new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.QueueServiceUri)}", "https://unit-test/queue"),
                        new KeyValuePair<string, string>($"{connectionName}:{nameof(AzureStorageAccountOptions.TableServiceUri)}", "https://unit-test/table"),
                    })
                .Build();

            AzureStorageAccountOptions actual = config.GetSection(connectionName).Get<AzureStorageAccountOptions>();
            Assert.Equal("MyAccount", actual.AccountName);
            Assert.Equal(new Uri("https://unit-test/blob", UriKind.Absolute), actual.BlobServiceUri);
            Assert.Equal(new Uri("https://unit-test/queue", UriKind.Absolute), actual.QueueServiceUri);
            Assert.Equal(new Uri("https://unit-test/table", UriKind.Absolute), actual.TableServiceUri);
        }
#endif
    }
}