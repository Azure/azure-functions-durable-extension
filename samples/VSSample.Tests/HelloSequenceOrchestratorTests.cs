// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Moq;
    using Xunit;

    public class HelloSequenceTests
    {
        [Fact]
        public async Task Run_returns_multiple_greetings()
        {
            var durableOrchestrationContextMock = new Mock<DurableOrchestrationContextBase>();
            durableOrchestrationContextMock.Setup(x => x.CallActivityAsync<string>("E1_SayHello", "Tokyo")).ReturnsAsync("Hello Tokyo");
            durableOrchestrationContextMock.Setup(x => x.CallActivityAsync<string>("E1_SayHelloPlusSeattle", "Hello Tokyo")).ReturnsAsync("Hello Tokyo and Seattle");
            durableOrchestrationContextMock.Setup(x => x.CallActivityAsync<string>("E1_SayHelloPlusLondon", "Hello Tokyo and Seattle")).ReturnsAsync("Hello Tokyo and Seattle and London!");

            var result = await HelloSequence.Run(durableOrchestrationContextMock.Object);

            Assert.Equal("Hello Tokyo and Seattle and London!", result);
        }
    }
}
