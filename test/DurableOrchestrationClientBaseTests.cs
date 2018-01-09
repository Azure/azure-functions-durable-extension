using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Azure.WebJobs;
using Moq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationClientBaseTests
    {
        [Fact]
        public async Task StartNewAsync_is_calling_overload_method()
        {
            var instanceId = Guid.NewGuid().ToString();
            const string functionName = "sampleFunction";
            var durableOrchestrationClientBaseMock = new Mock<DurableOrchestrationClientBase> {CallBase = true};
            durableOrchestrationClientBaseMock.Setup(x => x.StartNewAsync(functionName, string.Empty, null)).ReturnsAsync(instanceId);

            var result = await durableOrchestrationClientBaseMock.Object.StartNewAsync(functionName, null);
            result.Should().Be(instanceId);
        }
    }
}
