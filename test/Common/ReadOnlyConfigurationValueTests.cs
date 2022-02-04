// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class ReadOnlyConfigurationValueTests
    {
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("Foo", "Foo", "Bar")]
        [InlineData("Foo:Bar", "Bar", null)]
        [InlineData("Resources:Greetings:Default", "Default", "Hello World")]
        public void Creation(string path, string key, string value)
        {
            var actual = new ReadOnlyConfigurationValue(path, value);
            Assert.Equal(path, actual.Path);
            Assert.Equal(key, actual.Key);
            Assert.Equal(value, actual.Value);
            Assert.Null(actual["Any string"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ReadOnly()
        {
            var actual = new ReadOnlyConfigurationValue("Foo", "Bar");
            Assert.Throws<NotSupportedException>(() => actual.Value = "Baz");
            Assert.Throws<NotSupportedException>(() => actual["Baz"] = "Value");
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetChildren()
        {
            Assert.Empty(new ReadOnlyConfigurationValue("Foo", "Bar").GetChildren());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetReloadToken()
        {
            var actual = new ReadOnlyConfigurationValue("Foo", "Bar");
            IChangeToken token = actual.GetReloadToken();

            Assert.True(token.ActiveChangeCallbacks);
            Assert.False(token.HasChanged);

            // Ensure no errors
            token.RegisterChangeCallback(o => { }, null).Dispose();
        }
    }
}