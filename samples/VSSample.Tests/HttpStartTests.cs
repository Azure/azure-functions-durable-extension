using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace VSSample.Tests
{
    public class HttpStartTests
    {
        [Fact]
        public async Task HttpStart_returns_retryafter_header()
        {
            // Define constants
            const string functionName = "SampleFunction";
            const string instanceId = "7E467BDB-213F-407A-B86A-1954053D3C24";

            // Mock TraceWriter
            var traceWriterMock = new Mock<TraceWriter>(TraceLevel.Info);

            // Mock DurableOrchestrationClientBase
            var durableOrchestrationClientBaseMock = new Mock<DurableOrchestrationClientBase>();

            // Mock StartNewAsync method
            durableOrchestrationClientBaseMock.
                Setup(x => x.StartNewAsync(functionName, It.IsAny<object>())).
                ReturnsAsync(instanceId);

            // Mock CreateCheckStatusResponse method
            durableOrchestrationClientBaseMock
                .Setup(x => x.CreateCheckStatusResponse(It.IsAny<HttpRequestMessage>(), instanceId))
                .Returns(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(string.Empty),
                });

            // Call Orchestration trigger function
            var result = await HttpStart.Run(
                new HttpRequestMessage()
                {
                    Content = new StringContent(JsonConvert.SerializeObject(string.Empty), Encoding.UTF8, "application/json"),
                    RequestUri = new Uri("https://www.microsoft.com/"),
                },
                durableOrchestrationClientBaseMock.Object,
                functionName,
                traceWriterMock.Object);

            // Validate that output is not null
            result.Headers.RetryAfter.Should().NotBeNull();

            // Validate output's Retry-After header value
            result.Headers.RetryAfter.Delta.Should().Be(TimeSpan.FromSeconds(10));
        }
    }
}
