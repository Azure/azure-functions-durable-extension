// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class StandardConnectionInfoProviderTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Resolve()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new KeyValuePair<string, string>[]
                    {
                        new KeyValuePair<string, string>($"MyConnectionString", "Foo=Bar;Baz"),
                        new KeyValuePair<string, string>($"MyConnection:AccountName", "MyAccount"),
                    })
                .Build();

            var provider = new StandardConnectionInfoProvider(config);

            Assert.Equal("Foo=Bar;Baz", provider.Resolve("MyConnectionString").Value);
            Assert.Equal("MyAccount", provider.Resolve("MyConnection")["AccountName"]);
        }
    }
}