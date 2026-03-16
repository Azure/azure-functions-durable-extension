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
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                new KeyValuePair<string, string>[]
                    {
                        new KeyValuePair<string, string>(AddPrefix("MyConnectionString", prefixNames), "Foo=Bar;Baz"),
                        new KeyValuePair<string, string>($"ConnectionStrings:{AddPrefix("MyOtherConnectionString", prefixNames)}", "https://foo.bar/baz"),
                        new KeyValuePair<string, string>($"{AddPrefix("MyConnection", prefixNames)}:AccountName", "MyAccount"),
                        new KeyValuePair<string, string>($"ConnectionStrings:{AddPrefix("MyOtherConnection", prefixNames)}:AccountName", "MyOtherAccount"),
                    })
                .Build();

            var provider = new WebJobsConnectionInfoProvider(config);

            Assert.Equal("Foo=Bar;Baz", provider.Resolve("MyConnectionString").Value);
            Assert.Equal("https://foo.bar/baz", provider.Resolve("MyOtherConnectionString").Value);
            Assert.Equal("MyAccount", provider.Resolve("MyConnection")["AccountName"]);
            Assert.Equal("MyOtherAccount", provider.Resolve("MyOtherConnection")["AccountName"]);
        }

        private static string AddPrefix(string name, bool prefix) =>
            prefix ? "AzureWebJobs" + name : name;
    }
}