// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
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
        public void Resolve(bool prefixNames)
        {
#if FUNCTIONS_V1
            // Instead of mocking ConfigurationManager.ConnectionStrings, use environment variables
            string connectionName = Guid.NewGuid().ToString();
            string previousValue = Environment.GetEnvironmentVariable(AddPrefix(connectionName, prefixNames), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(AddPrefix(connectionName, prefixNames), "Foo=Bar;Baz", EnvironmentVariableTarget.Process);

            try
            {
                var provider = new WebJobsConnectionInfoProvider();
                Assert.Equal("Foo=Bar;Baz", provider.Resolve(connectionName).Value);
                Assert.Null(provider.Resolve(Guid.NewGuid().ToString()).Value);
            }
            finally
            {
                Environment.SetEnvironmentVariable(AddPrefix(connectionName, prefixNames), previousValue, EnvironmentVariableTarget.Process);
            }
#else
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                new KeyValuePair<string, string>[]
                    {
                        new KeyValuePair<string, string>(AddPrefix("MyConnectionString", prefixNames), "Foo=Bar;Baz"),
                        new KeyValuePair<string, string>($"ConnectionStrings:{AddPrefix("MyOtherConnectionString", prefixNames)}", "https://foo.bar/baz"),
                        new KeyValuePair<string, string>($"{AddPrefix("MyConnection", prefixNames)}:{nameof(AzureStorageAccountOptions.AccountName)}", "MyAccount"),
                        new KeyValuePair<string, string>($"ConnectionStrings:{AddPrefix("MyOtherConnection", prefixNames)}:{nameof(AzureStorageAccountOptions.AccountName)}", "MyOtherAccount"),
                    })
                .Build();

            var provider = new WebJobsConnectionInfoProvider(config);

            Assert.Equal("Foo=Bar;Baz", provider.Resolve("MyConnectionString").Value);
            Assert.Equal("https://foo.bar/baz", provider.Resolve("MyOtherConnectionString").Value);
            Assert.Equal("MyAccount", provider.Resolve("MyConnection")[nameof(AzureStorageAccountOptions.AccountName)]);
            Assert.Equal("MyOtherAccount", provider.Resolve("MyOtherConnection")[nameof(AzureStorageAccountOptions.AccountName)]);
#endif
        }

        private static string AddPrefix(string name, bool prefix) =>
            prefix ? "AzureWebJobs" + name : name;
    }
}