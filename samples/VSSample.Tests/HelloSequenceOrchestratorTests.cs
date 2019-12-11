// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs.Extensions.DurableTask;
    using Moq;
    using Xunit;

    public class HelloSequenceTests
    {
        [Fact]
        public async Task Run_returns_multiple_greetings()
        {
            var mockContext = new Mock<IDurableOrchestrationContext>();
            mockContext.Setup(x => x.CallActivityAsync<string>("E1_SayHello", "Tokyo")).ReturnsAsync("Hello Tokyo!");
            mockContext.Setup(x => x.CallActivityAsync<string>("E1_SayHello", "Seattle")).ReturnsAsync("Hello Seattle!");
            mockContext.Setup(x => x.CallActivityAsync<string>("E1_SayHello_DirectInput", "London")).ReturnsAsync("Hello London!");

            var result = await HelloSequence.Run(mockContext.Object);

            Assert.Equal(3, result.Count);
            Assert.Equal("Hello Tokyo!", result[0]);
            Assert.Equal("Hello Seattle!", result[1]);
            Assert.Equal("Hello London!", result[2]);
        }
    }
}
