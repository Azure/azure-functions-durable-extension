using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace VSSample.Tests
{
    [TestClass]
    public class HelloSequenceTests
    {
        [TestMethod]
        public void SayHello_returns_greeting()
        {
            var result = HelloSequence.SayHello("John");
            Assert.AreEqual(result, "Hello John!");
        }

        [TestMethod]
        public async Task Run_retuns_multiple_greetings()
        {
            var durableOrchestrationContextMock = new Mock<DurableOrchestrationContextBase>();
            durableOrchestrationContextMock.Setup(x => x.CallActivityAsync<string>("E1_SayHello", "Tokyo")).ReturnsAsync("Hello Tokyo!");
            durableOrchestrationContextMock.Setup(x => x.CallActivityAsync<string>("E1_SayHello", "Seattle")).ReturnsAsync("Hello Seattle!");
            durableOrchestrationContextMock.Setup(x => x.CallActivityAsync<string>("E1_SayHello", "London")).ReturnsAsync("Hello London!");

            var result = await HelloSequence.Run(durableOrchestrationContextMock.Object);

            Assert.AreEqual(result.Count, 3);
            Assert.AreEqual(result[0], "Hello Tokyo!");
            Assert.AreEqual(result[1], "Hello Seattle!");
            Assert.AreEqual(result[2], "Hello London!");
        }
    }
}
