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
                        new KeyValuePair<string, string>($"{connectionName}:AccountName", "MyAccount"),
                        new KeyValuePair<string, string>($"{connectionName}:BlobServiceUri", "https://unit-test/blob"),
                        new KeyValuePair<string, string>($"{connectionName}:QueueServiceUri", "https://unit-test/queue"),
                        new KeyValuePair<string, string>($"{connectionName}:TableServiceUri", "https://unit-test/table"),
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