// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace VSSample.Tests
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Host;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class HttpSyncStartTests
    {
        private const string FunctionName = "SampleFunction";
        private const string EventData = "EventData";
        private const string InstanceId = "7E467BDB-213F-407A-B86A-1954053D3C24";

        [Fact]
        public async Task Run_uses_default_values_when_query_parameters_missing()
        {
            await Check_behavior_based_on_query_parameters("http://localhost:7071/orchestrators/E1_HelloSequence", null, null);
        }

        [Fact]
        public async Task Run_uses_default_value_for_timeout()
        {
            await Check_behavior_based_on_query_parameters("http://localhost:7071/orchestrators/E1_HelloSequence?retryInterval=2", null, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task Run_uses_default_value_for_retryInterval()
        {
            await Check_behavior_based_on_query_parameters("http://localhost:7071/orchestrators/E1_HelloSequence?timeout=6", TimeSpan.FromSeconds(6), null);
        }

        [Fact]
        public async Task Run_uses_query_parameters()
        {
            await Check_behavior_based_on_query_parameters("http://localhost:7071/orchestrators/E1_HelloSequence?timeout=6&retryInterval=2", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(2));
        }

        private static async Task Check_behavior_based_on_query_parameters(string url, TimeSpan? timeout, TimeSpan? retryInterval)
        {
            var name = new Name { First = "John", Last = "Smith" };
            var request = new HttpRequestMessage
            {
                Content = new StringContent(JsonConvert.SerializeObject(name), Encoding.UTF8, "application/json"),
                RequestUri = new Uri(url)
            };
            var traceWriterMock = new Mock<TraceWriter>(TraceLevel.Info);
            var durableOrchestrationClientBaseMock = new Mock<DurableOrchestrationClientBase> { CallBase = true };
            durableOrchestrationClientBaseMock.
                Setup(x => x.StartNewAsync(FunctionName, It.IsAny<object>())).
                ReturnsAsync(InstanceId);
            durableOrchestrationClientBaseMock
                .Setup(x => x.WaitForCompletionOrCreateCheckStatusResponseAsync(request, InstanceId, timeout, retryInterval))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(EventData)
                });
            var result = await HttpSyncStart.Run(request, durableOrchestrationClientBaseMock.Object, FunctionName, traceWriterMock.Object);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(EventData, await result.Content.ReadAsStringAsync());
        }
    }

    public class Name
    {
        public string First { get; set; }
        public string Last { get; set; }
    }
}
