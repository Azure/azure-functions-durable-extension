// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

extern alias HostExtension;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using HostRequest = HostExtension::Microsoft.Azure.WebJobs.Extensions.DurableTask.DurableHttpRequest;
using HostTokenSource = HostExtension::Microsoft.Azure.WebJobs.Extensions.DurableTask.ManagedIdentityTokenSource;
using WorkerMethods = Microsoft.DurableTask.TaskOrchestrationContextExtensionMethods;
using WorkerRequest = Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http.DurableHttpRequest;
using WorkerResponse = Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http.DurableHttpResponse;
using WorkerTokenSource = Microsoft.Azure.Functions.Worker.Extensions.DurableTask.ManagedIdentityTokenSource;

namespace Microsoft.Azure.Functions.Worker.Tests
{
    public class PollingWireCompatibilityTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WorkerRequestsDeserializeInHostAcrossPollingHops(bool restricted)
        {
            var request = new WorkerRequest(
                HttpMethod.Post,
                new Uri("https://start.example/start"),
                new Dictionary<string, StringValues>
                {
                    { "X-Selected", "value" },
                    { "Authorization", "Bearer test" },
                    { "Cookie", "test=value" },
                    { "x-functions-key", "test-key" },
                },
                asynchronousPatternEnabled: true)
            {
                EnableCrossOriginHeaderFiltering = restricted,
                CrossOriginPollingHeaders = new Dictionary<string, string[]>
                {
                    { "https://poll.example", new[] { "x-selected", "Authorization", "Cookie", "x-functions-key" } },
                },
                TokenSource = new WorkerTokenSource("https://start.example/"),
            };
            var responses = new Queue<WorkerResponse>(new[]
            {
                Accepted("https://poll.example/first"),
                Accepted("https://other.example/next"),
                new WorkerResponse(HttpStatusCode.OK),
            });
            var hostRequests = new List<HostRequest>();
            var context = new Mock<TaskOrchestrationContext>();
            context.Setup(c => c.CreateReplaySafeLogger(It.IsAny<string>())).Returns(NullLogger.Instance);
            context.Setup(c => c.Properties).Returns(new Dictionary<string, object?>());
            context.Setup(c => c.CreateTimer(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            context.Setup(c => c.CallActivityAsync<WorkerResponse>(
                It.IsAny<TaskName>(), It.IsAny<object>(), It.IsAny<TaskOptions>()))
                .Callback<TaskName, object?, TaskOptions?>((_, input, _) =>
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<WorkerRequest>(input));
                    Assert.DoesNotContain("disablePollingRedirects", json);
                    hostRequests.Add(JsonConvert.DeserializeObject<HostRequest>(json) ?? throw new InvalidOperationException("Request was null."));
                })
                .ReturnsAsync(() => responses.Dequeue());

            WorkerResponse response = await WorkerMethods.CallHttpAsync(context.Object, request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, hostRequests.Count);
            Assert.NotNull(hostRequests[0].CrossOriginPollingHeaders);
            Assert.IsType<HostTokenSource>(hostRequests[0].TokenSource);
            Assert.Equal(restricted, hostRequests[0].EnableCrossOriginHeaderFiltering);
            Assert.Null(JObject.Parse(JsonConvert.SerializeObject(hostRequests[0]))["disablePollingRedirects"]);
            Assert.Single(hostRequests[1].Headers);
            Assert.Equal("value", hostRequests[1].Headers["X-Selected"]);
            Assert.Null(hostRequests[1].TokenSource);
            Assert.Null(JObject.Parse(JsonConvert.SerializeObject(hostRequests[1]))["disablePollingRedirects"]);
            Assert.Equal(restricted ? 0 : 1, hostRequests[2].Headers.Count);
            Assert.Null(hostRequests[2].TokenSource);
            Assert.Null(JObject.Parse(JsonConvert.SerializeObject(hostRequests[2]))["disablePollingRedirects"]);
            Assert.NotNull(request.Headers);
            Assert.Equal(4, request.Headers.Count);
        }

        private static WorkerResponse Accepted(string location)
        {
            return new WorkerResponse(HttpStatusCode.Accepted)
            {
                Headers = new Dictionary<string, StringValues> { { "Location", location } },
            };
        }
    }
}
