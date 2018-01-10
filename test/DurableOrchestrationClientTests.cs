// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
using FluentAssertions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Moq;
using Xunit;

namespace WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationClientTests
    {
        [Fact]
        public async Task CreateCheckStatusResponse_calls_DurableTaskExtension_method()
        {

            var durableOrchestrationClient = GetDurableOrchestrationClient();
            var result = durableOrchestrationClient.CreateCheckStatusResponse(new HttpRequestMessage(), TestConstants.InstanceIdDurableOrchestrationClientTests);
            result.StatusCode.Should().Be(HttpStatusCode.OK);
            (await result.Content.ReadAsStringAsync()).Should().Be(TestConstants.SampleData);
        }

        [Fact]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_calls_DurableTaskExtension_method()
        {
            var durableOrchestrationClient = GetDurableOrchestrationClient();
            var result = await durableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(new HttpRequestMessage(), TestConstants.InstanceIdDurableOrchestrationClientTests, null, null);
            result.StatusCode.Should().Be(HttpStatusCode.OK);
            (await result.Content.ReadAsStringAsync()).Should().Be(TestConstants.SampleData);
        }

        private DurableOrchestrationClient GetDurableOrchestrationClient()
        {
            var serviceClientMock = new Mock<IOrchestrationServiceClient>();
            var traceWriteMock = new Mock<TraceWriter>(TraceLevel.Info);
            var endToEndTraceHelperMock =
                new EndToEndTraceHelperMock(new JobHostConfiguration(), traceWriteMock.Object);
            var durableOrchestrationClient = new DurableOrchestrationClient(
                serviceClientMock.Object,
                new DurableTaskExtensionMock(),
                new OrchestrationClientAttribute(),
                endToEndTraceHelperMock);
            return durableOrchestrationClient;
        }
    }
}
