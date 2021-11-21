// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
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
    }
}