﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class HttpApiHandlerTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        private void CreateCheckStatusResponse_Throws_Exception_When_NotificationUrl_Missing()
        {
            var options = new DurableTaskOptions();
            options.NotificationUrl = null;

            var httpApiHandler = new HttpApiHandler(GetTestExtension(options), null);
            var ex = Assert.Throws<InvalidOperationException>(() => httpApiHandler.CreateCheckStatusResponse(new HttpRequestMessage(), string.Empty, null));
            Assert.Equal("Webhooks are not configured", ex.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Throws_Exception_When_Bad_Timeout_Request()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(TestConstants.RequestUri),
                },
                TestConstants.InstanceId,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName,
                },
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(100)));
            Assert.Equal($"Total timeout 0 should be bigger than retry timeout 100", ex.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CreateCheckStatusResponse_Returns_Corrent_HTTP_202_Response()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            var httpResponseMessage = httpApiHandler.CreateCheckStatusResponse(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(TestConstants.RequestUri),
                },
                TestConstants.InstanceId,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName,
                });

            Assert.Equal(HttpStatusCode.Accepted, httpResponseMessage.StatusCode);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var status = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(status["id"], TestConstants.InstanceId);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742?taskHub=SampleHubVS&connection=Storage&code=mykey",
                status["statusQueryGetUri"]);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/raiseEvent/{{eventName}}?taskHub=SampleHubVS&connection=Storage&code=mykey",
                status["sendEventPostUri"]);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/terminate?reason={{text}}&taskHub=SampleHubVS&connection=Storage&code=mykey",
                status["terminatePostUri"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateCheckStatus_Returns_Corrent_HttpManagementPayload_based_on_default_values()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            HttpManagementPayload httpManagementPayload = httpApiHandler.CreateHttpManagementPayload(TestConstants.InstanceId, null, null);
            Assert.NotNull(httpManagementPayload);
            Assert.Equal(httpManagementPayload.Id, TestConstants.InstanceId);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742?taskHub=DurableFunctionsHub&connection=Storage&code=mykey",
                httpManagementPayload.StatusQueryGetUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/raiseEvent/{{eventName}}?taskHub=DurableFunctionsHub&connection=Storage&code=mykey",
                httpManagementPayload.SendEventPostUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/terminate?reason={{text}}&taskHub=DurableFunctionsHub&connection=Storage&code=mykey",
                httpManagementPayload.TerminatePostUri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateCheckStatus_Returns_Corrent_HttpManagementPayload_based_on_custom_taskhub_value()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            HttpManagementPayload httpManagementPayload = httpApiHandler.CreateHttpManagementPayload(TestConstants.InstanceId, TestConstants.TaskHub, null);
            Assert.NotNull(httpManagementPayload);
            Assert.Equal(httpManagementPayload.Id, TestConstants.InstanceId);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742?taskHub=SampleHubVS&connection=Storage&code=mykey",
                httpManagementPayload.StatusQueryGetUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/raiseEvent/{{eventName}}?taskHub=SampleHubVS&connection=Storage&code=mykey",
                httpManagementPayload.SendEventPostUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/terminate?reason={{text}}&taskHub=SampleHubVS&connection=Storage&code=mykey",
                httpManagementPayload.TerminatePostUri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateCheckStatus_Returns_Corrent_HttpManagementPayload_based_on_custom_connection_value()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            HttpManagementPayload httpManagementPayload = httpApiHandler.CreateHttpManagementPayload(TestConstants.InstanceId, null, TestConstants.CustomConnectionName);
            Assert.NotNull(httpManagementPayload);
            Assert.Equal(httpManagementPayload.Id, TestConstants.InstanceId);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742?taskHub=DurableFunctionsHub&connection=TestConnection&code=mykey",
                httpManagementPayload.StatusQueryGetUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/raiseEvent/{{eventName}}?taskHub=DurableFunctionsHub&connection=TestConnection&code=mykey",
                httpManagementPayload.SendEventPostUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/terminate?reason={{text}}&taskHub=DurableFunctionsHub&connection=TestConnection&code=mykey",
                httpManagementPayload.TerminatePostUri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CreateCheckStatus_Returns_Corrent_HttpManagementPayload_based_on_custom_values()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            HttpManagementPayload httpManagementPayload = httpApiHandler.CreateHttpManagementPayload(TestConstants.InstanceId, TestConstants.TaskHub, TestConstants.CustomConnectionName);
            Assert.NotNull(httpManagementPayload);
            Assert.Equal(httpManagementPayload.Id, TestConstants.InstanceId);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742?taskHub=SampleHubVS&connection=TestConnection&code=mykey",
                httpManagementPayload.StatusQueryGetUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/raiseEvent/{{eventName}}?taskHub=SampleHubVS&connection=TestConnection&code=mykey",
                httpManagementPayload.SendEventPostUri);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/7b59154ae666471993659902ed0ba742/terminate?reason={{text}}&taskHub=SampleHubVS&connection=TestConnection&code=mykey",
                httpManagementPayload.TerminatePostUri);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_202_Response_After_Timeout()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            var stopWatch = Stopwatch.StartNew();
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(TestConstants.RequestUri),
                },
                TestConstants.RandomInstanceId,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName,
                },
                TimeSpan.FromSeconds(100),
                TimeSpan.FromSeconds(10));
            stopWatch.Stop();
            Assert.Equal(HttpStatusCode.Accepted, httpResponseMessage.StatusCode);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var status = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(status["id"], TestConstants.RandomInstanceId);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/9b59154ae666471993659902ed0ba749?taskHub=SampleHubVS&connection=Storage&code=mykey",
                status["statusQueryGetUri"]);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/9b59154ae666471993659902ed0ba749/raiseEvent/{{eventName}}?taskHub=SampleHubVS&connection=Storage&code=mykey",
                status["sendEventPostUri"]);
            Assert.Equal(
                $"{TestConstants.NotificationUrlBase}/instances/9b59154ae666471993659902ed0ba749/terminate?reason={{text}}&taskHub=SampleHubVS&connection=Storage&code=mykey",
                status["terminatePostUri"]);
            Assert.True(stopWatch.Elapsed > TimeSpan.FromSeconds(30));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(TestConstants.RequestUri),
                },
                TestConstants.IntanceIdFactComplete,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName,
                },
                TimeSpan.FromSeconds(100),
                TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<string>(content);
            Assert.Equal("Hello Tokyo!", value);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_HTTP_200_Response_After_Few_Iterations()
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            var stopwatch = Stopwatch.StartNew();
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(TestConstants.RequestUri),
                },
                TestConstants.InstanceIdIterations,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName,
                },
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(8));
            stopwatch.Stop();
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var value = JsonConvert.DeserializeObject<string>(content);
            Assert.Equal("Hello Tokyo!", value);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_Defaults_When_Runtime_Status_is_Failed()
        {
            await this.CheckRuntimeStatus(TestConstants.InstanceIdFailed, OrchestrationRuntimeStatus.Failed, HttpStatusCode.InternalServerError);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_Defaults_When_Runtime_Status_is_Terminated()
        {
            await this.CheckRuntimeStatus(TestConstants.InstanceIdTerminated, OrchestrationRuntimeStatus.Terminated);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForCompletionOrCreateCheckStatusResponseAsync_Returns_Defaults_When_Runtime_Status_is_Canceled()
        {
            await this.CheckRuntimeStatus(TestConstants.InstanceIdCanceled, OrchestrationRuntimeStatus.Canceled);
        }

        private async Task CheckRuntimeStatus(string instanceId, OrchestrationRuntimeStatus runtimeStatus, HttpStatusCode httpStatusCode = HttpStatusCode.OK)
        {
            var httpApiHandler = new HttpApiHandler(GetTestExtension(), null);
            var httpResponseMessage = await httpApiHandler.WaitForCompletionOrCreateCheckStatusResponseAsync(
                new HttpRequestMessage
                {
                    RequestUri = new Uri(TestConstants.RequestUri),
                },
                instanceId,
                new OrchestrationClientAttribute
                {
                    TaskHub = TestConstants.TaskHub,
                    ConnectionName = TestConstants.ConnectionName,
                },
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(8));
            Assert.Equal(httpResponseMessage.StatusCode, httpStatusCode);
            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<JObject>(content);
            Assert.Equal(response["runtimeStatus"], runtimeStatus.ToString());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetAllStatus_is_Success()
        {
            var list = (IList<DurableOrchestrationStatus>)new List<DurableOrchestrationStatus>
            {
                new DurableOrchestrationStatus
                {
                    InstanceId = "01",
                    RuntimeStatus = OrchestrationRuntimeStatus.Running,
                },
                new DurableOrchestrationStatus
                {
                    InstanceId = "02",
                    RuntimeStatus = OrchestrationRuntimeStatus.Completed,
                },
            };

            var clientMock = new Mock<DurableOrchestrationClientBase>();
            clientMock
                .Setup(x => x.GetStatusAsync(default(DateTime), default(DateTime), new List<OrchestrationRuntimeStatus>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(list));
            var httpApiHandler = new ExtendedHttpApiHandler(clientMock.Object);

            var getStatusRequestUriBuilder = new UriBuilder(TestConstants.NotificationUrl);
            getStatusRequestUriBuilder.Path += $"/Instances/";

            var responseMessage = await httpApiHandler.HandleRequestAsync(
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = getStatusRequestUriBuilder.Uri,
                });
            Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
            Assert.Equal(string.Empty, responseMessage.Headers.GetValues("x-ms-continuation-token").FirstOrDefault());
            var actual = JsonConvert.DeserializeObject<IList<StatusResponsePayload>>(await responseMessage.Content.ReadAsStringAsync());

            Assert.Equal("01", actual[0].InstanceId);
            Assert.Equal("Running", actual[0].RuntimeStatus);
            Assert.Equal("02", actual[1].InstanceId);
            Assert.Equal("Completed", actual[1].RuntimeStatus);
        }

        [Fact]
        public async Task GetQueryStatus_is_Success()
        {
            var list = (IList<DurableOrchestrationStatus>)new List<DurableOrchestrationStatus>
            {
                new DurableOrchestrationStatus
                {
                    InstanceId = "01",
                    CreatedTime = new DateTime(2018, 3, 10, 10, 10, 10),
                    RuntimeStatus = OrchestrationRuntimeStatus.Running,
                },
                new DurableOrchestrationStatus
                {
                    InstanceId = "02",
                    CreatedTime = new DateTime(2018, 3, 10, 10, 6, 10),
                    RuntimeStatus = OrchestrationRuntimeStatus.Running,
                },
            };

            var createdTimeFrom = new DateTime(2018, 3, 10, 10, 1, 0);
            var createdTimeTo = new DateTime(2018, 3, 10, 10, 23, 59);
            var runtimeStatus = new List<OrchestrationRuntimeStatus>();
            runtimeStatus.Add(OrchestrationRuntimeStatus.Running);
            var runtimeStatusString = OrchestrationRuntimeStatus.Running.ToString();

            var clientMock = new Mock<DurableOrchestrationClientBase>();
            clientMock
                .Setup(x => x.GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(list));
            var httpApiHandler = new ExtendedHttpApiHandler(clientMock.Object);

            var getStatusRequestUriBuilder = new UriBuilder(TestConstants.NotificationUrl);
            getStatusRequestUriBuilder.Path += $"/Instances/";
            getStatusRequestUriBuilder.Query = $"createdTimeFrom={WebUtility.UrlEncode(createdTimeFrom.ToString())}&createdTimeTo={WebUtility.UrlEncode(createdTimeTo.ToString())}&runtimeStatus={runtimeStatusString}";

            var responseMessage = await httpApiHandler.HandleRequestAsync(
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = getStatusRequestUriBuilder.Uri,
                });
            Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
            var actual = JsonConvert.DeserializeObject<IList<StatusResponsePayload>>(await responseMessage.Content.ReadAsStringAsync());
            clientMock.Verify(x => x.GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus, It.IsAny<CancellationToken>()));
            Assert.Equal("01", actual[0].InstanceId);
            Assert.Equal("Running", actual[0].RuntimeStatus);
            Assert.Equal("02", actual[1].InstanceId);
            Assert.Equal("Running", actual[1].RuntimeStatus);
        }

        [Fact]
        public async Task GetQueryMultipleRuntimeStatus_is_Success()
        {
            var list = (IList<DurableOrchestrationStatus>)new List<DurableOrchestrationStatus>
            {
                new DurableOrchestrationStatus
                {
                    InstanceId = "01",
                    CreatedTime = new DateTime(2018, 3, 10, 10, 10, 10),
                    RuntimeStatus = OrchestrationRuntimeStatus.Running,
                },
                new DurableOrchestrationStatus
                {
                    InstanceId = "02",
                    CreatedTime = new DateTime(2018, 3, 10, 10, 6, 10),
                    RuntimeStatus = OrchestrationRuntimeStatus.Completed,
                },
            };

            var createdTimeFrom = new DateTime(2018, 3, 10, 10, 1, 0);
            var createdTimeTo = new DateTime(2018, 3, 10, 10, 23, 59);
            var runtimeStatus = new List<OrchestrationRuntimeStatus>();
            runtimeStatus.Add(OrchestrationRuntimeStatus.Running);
            runtimeStatus.Add(OrchestrationRuntimeStatus.Completed);

            var runtimeStatusRunningString = OrchestrationRuntimeStatus.Running.ToString();
            var runtimeStatusCompletedString = OrchestrationRuntimeStatus.Completed.ToString();

            var clientMock = new Mock<DurableOrchestrationClientBase>();
            clientMock
                .Setup(x => x.GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(list));
            var httpApiHandler = new ExtendedHttpApiHandler(clientMock.Object);

            var getStatusRequestUriBuilder = new UriBuilder(TestConstants.NotificationUrl);
            getStatusRequestUriBuilder.Path += $"/Instances/";
            getStatusRequestUriBuilder.Query = $"createdTimeFrom={WebUtility.UrlEncode(createdTimeFrom.ToString())}&createdTimeTo={WebUtility.UrlEncode(createdTimeTo.ToString())}&runtimeStatus={runtimeStatusRunningString},{runtimeStatusCompletedString}";

            var responseMessage = await httpApiHandler.HandleRequestAsync(
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = getStatusRequestUriBuilder.Uri,
                });
            Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
            var actual = JsonConvert.DeserializeObject<IList<StatusResponsePayload>>(await responseMessage.Content.ReadAsStringAsync());
            clientMock.Verify(x => x.GetStatusAsync(createdTimeFrom, createdTimeTo, runtimeStatus, It.IsAny<CancellationToken>()));
            Assert.Equal("01", actual[0].InstanceId);
            Assert.Equal("Running", actual[0].RuntimeStatus);
            Assert.Equal("02", actual[1].InstanceId);
            Assert.Equal("Completed", actual[1].RuntimeStatus);
        }

        [Fact]
        public async Task GetQueryWithoutRuntimeStatus_is_Success()
        {
            var list = (IList<DurableOrchestrationStatus>)new List<DurableOrchestrationStatus>
            {
                new DurableOrchestrationStatus
                {
                    InstanceId = "01",
                    CreatedTime = new DateTime(2018, 3, 10, 10, 10, 10),
                    RuntimeStatus = OrchestrationRuntimeStatus.Running,
                },
                new DurableOrchestrationStatus
                {
                    InstanceId = "02",
                    CreatedTime = new DateTime(2018, 3, 10, 10, 6, 10),
                    RuntimeStatus = OrchestrationRuntimeStatus.Completed,
                },
            };

            var createdTimeFrom = new DateTime(2018, 3, 10, 10, 1, 0);

            var clientMock = new Mock<DurableOrchestrationClientBase>();
            clientMock
                .Setup(x => x.GetStatusAsync(createdTimeFrom, default(DateTime), new List<OrchestrationRuntimeStatus>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(list));
            var httpApiHandler = new ExtendedHttpApiHandler(clientMock.Object);

            var getStatusRequestUriBuilder = new UriBuilder(TestConstants.NotificationUrl);
            getStatusRequestUriBuilder.Path += $"/Instances/";
            getStatusRequestUriBuilder.Query = $"createdTimeFrom={WebUtility.UrlEncode(createdTimeFrom.ToString())}";

            var responseMessage = await httpApiHandler.HandleRequestAsync(
                new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = getStatusRequestUriBuilder.Uri,
                });
            Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
            var actual = JsonConvert.DeserializeObject<IList<StatusResponsePayload>>(await responseMessage.Content.ReadAsStringAsync());
            clientMock.Verify(x => x.GetStatusAsync(createdTimeFrom, default(DateTime), new List<OrchestrationRuntimeStatus>(), It.IsAny<CancellationToken>()));
            Assert.Equal("01", actual[0].InstanceId);
            Assert.Equal("Running", actual[0].RuntimeStatus);
            Assert.Equal("02", actual[1].InstanceId);
            Assert.Equal("Completed", actual[1].RuntimeStatus);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TerminateInstanceWebhook()
        {
            string testInstanceId = Guid.NewGuid().ToString("N");
            string testReason = "TerminationReason" + Guid.NewGuid();

            string actualInstanceId = null;
            string actualReason = null;

            var clientMock = new Mock<DurableOrchestrationClientBase>();
            clientMock
                .Setup(x => x.TerminateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask)
                .Callback((string instanceId, string reason) =>
                {
                    actualInstanceId = instanceId;
                    actualReason = reason;
                });

            clientMock
                .Setup(x => x.GetStatusAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(
                    new DurableOrchestrationStatus
                    {
                        InstanceId = testInstanceId,
                        RuntimeStatus = OrchestrationRuntimeStatus.Running,
                    }));

            var terminateRequestUriBuilder = new UriBuilder(TestConstants.NotificationUrl);
            terminateRequestUriBuilder.Path += $"/Instances/{testInstanceId}/terminate";
            terminateRequestUriBuilder.Query = $"reason={testReason}&{terminateRequestUriBuilder.Query.TrimStart('?')}";

            var httpApiHandler = new ExtendedHttpApiHandler(clientMock.Object);
            await httpApiHandler.HandleRequestAsync(
                new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = terminateRequestUriBuilder.Uri,
                });

            Assert.Equal(testInstanceId, actualInstanceId);
            Assert.Equal(testReason, actualReason);
        }

        private static DurableTaskExtension GetTestExtension()
        {
            var options = new DurableTaskOptions();
            options.NotificationUrl = new Uri(TestConstants.NotificationUrl);

            return GetTestExtension(options);
        }

        private static DurableTaskExtension GetTestExtension(DurableTaskOptions options)
        {
            return new MockDurableTaskExtension(options);
        }

        // Same as regular HTTP Api handler except you can specify a custom client object.
        private class ExtendedHttpApiHandler : HttpApiHandler
        {
            private readonly DurableOrchestrationClientBase innerClient;

            public ExtendedHttpApiHandler(DurableOrchestrationClientBase client)
                : base(GetTestExtension(), null /* traceWriter */)
            {
                this.innerClient = client;
            }

            protected override DurableOrchestrationClientBase GetClient(OrchestrationClientAttribute attribute)
            {
                return this.innerClient;
            }
        }

        private class MockDurableTaskExtension : DurableTaskExtension
        {
            public MockDurableTaskExtension(DurableTaskOptions options)
                : base(
                    new OptionsWrapper<DurableTaskOptions>(options),
                    new LoggerFactory(),
                    TestHelpers.GetTestNameResolver(),
                    new TestConnectionStringResolver())
            {
            }

            protected internal override DurableOrchestrationClient GetClient(OrchestrationClientAttribute attribute)
            {
                var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
                return new DurableOrchestrationClientMock(orchestrationServiceClientMock.Object, this, null);
            }
        }
    }
}
