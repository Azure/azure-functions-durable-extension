// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample.Tests
{
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;
    using Xunit;
    using Moq;

    public class HelloSequenceActivityTests
    {
        [Fact]
        public void SayHello_returns_greeting()
        {
            var durableActivityContextMock = new Mock<IDurableActivityContext>();
            durableActivityContextMock.Setup(x => x.GetInput<string>()).Returns("John");
            var result = HelloSequence.SayHello(durableActivityContextMock.Object);
            Assert.Equal("Hello John!", result);
        }

        [Fact]
        public void SayHello_returns_greeting_direct_input()
        {
            var result = HelloSequence.SayHelloDirectInput("John");
            Assert.Equal("Hello John!", result);
        }
    }
}
