// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample.Tests
{
    using Xunit;

    public class HelloSequenceActivityTests
    {
        [Fact]
        public void SayHello_returns_greeting()
        {
            var result = HelloSequence.SayHello("there!");
            Assert.Equal("Hello there!", result);
        }

        [Fact]
        public void SayHelloPlusSeattle_returns_Seattle_attatched()
        {
            var result = HelloSequence.SayHelloPlusSeattle("Added:");
            Assert.Equal("Added: and Seattle", result);
        }

        [Fact]
        public void SayHelloPlusLondon_returns_London_attatched()
        {
            var result = HelloSequence.SayHelloPlusLondon("Added:");
            Assert.Equal("Added: and London!", result);
        }
    }
}
