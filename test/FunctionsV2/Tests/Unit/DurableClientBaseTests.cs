// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests.HttpApiHandlerTests;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableClientBaseTests
    {
        private readonly ITestOutputHelper output;

        public DurableClientBaseTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("@invalid")]
        [InlineData("/invalid")]
        [InlineData("invalid\\")]
        [InlineData("invalid#")]
        [InlineData("invalid?")]
        [InlineData("invalid\t")]
        [InlineData("invalid\n")]
        public async Task StartNewAsync_InvalidInstanceId_ThrowsException(string instanceId)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(GetInvalidInstanceState());
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();
            var durableClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await durableClient.StartNewAsync("anyOrchestratorFunction", instanceId, new { message = "any obj" }));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("durabletaskhub")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task StartNewAsync_DisabledOrchestrator_ThrowsException(string taskHub)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock
                .Setup(x => x.CreateTaskOrchestrationAsync(It.IsAny<TaskMessage>(), It.IsAny<OrchestrationStatus[]>()))
                .Returns(Task.CompletedTask);
            var storageProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClientMock.Object,
                TestConstants.ConnectionName);
            var durableExtension = GetDurableTaskConfig();
            durableExtension.RegisterOrchestrator(new FunctionName("DisabledOrchestrator"), orchestratorInfo: null);
            var durableClient = (IDurableOrchestrationClient)new DurableClient(
                storageProvider,
                durableExtension,
                durableExtension.HttpApiHandler,
                new DurableClientAttribute { TaskHub = taskHub });

            ArgumentException exception = await Assert.ThrowsAnyAsync<ArgumentException>(
                () => durableClient.StartNewAsync("DisabledOrchestrator"));

            Assert.Contains("doesn't exist, is disabled, or is not an orchestrator function", exception.Message);
            orchestrationServiceClientMock.Verify(
                x => x.CreateTaskOrchestrationAsync(It.IsAny<TaskMessage>(), It.IsAny<OrchestrationStatus[]>()),
                Times.Never());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("durabletaskhub")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RestartAsync_DisabledOrchestrator_ThrowsException(string taskHub)
        {
            const string InstanceId = "completed-instance";
            const string FunctionName = "DisabledOrchestrator";
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock
                .Setup(x => x.GetOrchestrationStateAsync(InstanceId, false))
                .ReturnsAsync(
                    new List<OrchestrationState>
                    {
                        new OrchestrationState
                        {
                            Name = FunctionName,
                            Input = "null",
                            OrchestrationInstance = new OrchestrationInstance { InstanceId = InstanceId },
                            OrchestrationStatus = OrchestrationStatus.Completed,
                        },
                    });
            orchestrationServiceClientMock
                .Setup(x => x.CreateTaskOrchestrationAsync(It.IsAny<TaskMessage>(), It.IsAny<OrchestrationStatus[]>()))
                .Returns(Task.CompletedTask);
            var storageProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClientMock.Object,
                TestConstants.ConnectionName);
            var durableExtension = GetDurableTaskConfig();
            durableExtension.RegisterOrchestrator(new FunctionName(FunctionName), orchestratorInfo: null);
            var durableClient = (IDurableOrchestrationClient)new DurableClient(
                storageProvider,
                durableExtension,
                durableExtension.HttpApiHandler,
                new DurableClientAttribute { TaskHub = taskHub });

            ArgumentException exception = await Assert.ThrowsAnyAsync<ArgumentException>(
                () => durableClient.RestartAsync(InstanceId, restartWithNewInstanceId: false));

            Assert.Contains("doesn't exist, is disabled, or is not an orchestrator function", exception.Message);
            orchestrationServiceClientMock.Verify(
                x => x.CreateTaskOrchestrationAsync(It.IsAny<TaskMessage>(), It.IsAny<OrchestrationStatus[]>()),
                Times.Never());
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("@invalid")]
        [InlineData("/invalid")]
        [InlineData("invalid\\")]
        [InlineData("invalid#")]
        [InlineData("invalid?")]
        [InlineData("invalid\t")]
        [InlineData("invalid\n")]
        public async Task SignalEntity_InvalidEntityKey_ThrowsException(string entityKey)
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(GetInvalidInstanceState());
            var storageProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClientMock.Object,
                TestConstants.ConnectionName);
            var durableExtension = GetDurableTaskConfig();
            var durableClient = (IDurableEntityClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            var entityId = new EntityId("test", entityKey);
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await durableClient.SignalEntityAsync(entityId, "test"));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RaiseEventAsync_InvalidInstanceId_ThrowsException(bool checkStatusBeforeRaiseEvent)
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(GetInvalidInstanceState());
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test", checkStatusBeforeRaiseEvent);
            var durableExtension = GetDurableTaskConfig();
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });
            Task RaiseEvent() => durableOrchestrationClient.RaiseEventAsync("invalid_instance_id", "anyEvent", new { message = "any message" });

            if (checkStatusBeforeRaiseEvent)
            {
                await Assert.ThrowsAnyAsync<ArgumentException>(RaiseEvent);
            }
            else
            {
                await RaiseEvent(); // no exception
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RaiseEventAsync_NonRunningFunction_ThrowsException(bool checkStatusBeforeRaiseEvent)
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Completed));
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test", checkStatusBeforeRaiseEvent);
            var durableExtension = GetDurableTaskConfig();
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            Task RaiseEvent() => durableOrchestrationClient.RaiseEventAsync("valid_instance_id", "anyEvent", new { message = "any message" });

            if (checkStatusBeforeRaiseEvent)
            {
                await Assert.ThrowsAnyAsync<InvalidOperationException>(RaiseEvent);
            }
            else
            {
                await RaiseEvent(); // no exception
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TerminateAsync_InvalidInstanceId_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInvalidInstanceState());
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await durableOrchestrationClient.TerminateAsync("invalid_instance_id", "any reason"));
            orchestrationServiceClientMock.Verify(x => x.ForceTerminateTaskOrchestrationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TerminateAsync_RunningOrchestrator_TerminateEventPlaced()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            await durableOrchestrationClient.TerminateAsync("valid_instance_id", "any reason");
            orchestrationServiceClientMock.Verify(x => x.ForceTerminateTaskOrchestrationAsync("valid_instance_id", "any reason"), Times.Once());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TerminateAsync_NonRunningOrchestrator_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Completed));
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await durableOrchestrationClient.TerminateAsync("invalid_instance_id", "any reason"));
            orchestrationServiceClientMock.Verify(x => x.ForceTerminateTaskOrchestrationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task StartNewAsync_LogsKnownTargetInstanceId()
        {
            var serviceClient = new Mock<IOrchestrationServiceClient>();
            (IDurableClient client, TestLogger logger) = this.CreateClientWithCapturedLogs(serviceClient.Object);

            await client.StartNewAsync("ChildOrchestrator", "child-id");

            Assert.Equal("child-id", GetLoggedTargetInstanceId(logger));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task SignalEntityAsync_LogsKnownTargetInstanceId()
        {
            var serviceClient = new Mock<IOrchestrationServiceClient>();
            (IDurableClient client, TestLogger logger) = this.CreateClientWithCapturedLogs(serviceClient.Object);
            var entityId = new EntityId("Counter", "entity-key");

            await client.SignalEntityAsync(entityId, "add");

            Assert.Equal(EntityId.GetSchedulerIdFromEntityId(entityId), GetLoggedTargetInstanceId(logger));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RaiseEventAsync_LogsKnownTargetInstanceId()
        {
            var serviceClient = new Mock<IOrchestrationServiceClient>();
            serviceClient
                .Setup(client => client.GetOrchestrationStateAsync("valid_instance_id", false))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));
            (IDurableClient client, TestLogger logger) = this.CreateClientWithCapturedLogs(serviceClient.Object);

            await client.RaiseEventAsync("valid_instance_id", "approval");

            Assert.Equal("valid_instance_id", GetLoggedTargetInstanceId(logger));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableClient_ExternalApp_StartNewAsync_ReturnsInstanceId()
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));

            var durableOrchestrationClient = this.GetDurableClient(orchestrationServiceClientMock.Object);

            var response = await durableOrchestrationClient.StartNewAsync("orchestrationName", "testInstanceId");
            Assert.Equal("testInstanceId", response);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableClient_ExternalApp_GetStatusAsync_ReturnsStatus()
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));

            var durableOrchestrationClient = this.GetDurableClient(orchestrationServiceClientMock.Object);
            var status = await durableOrchestrationClient.GetStatusAsync("testInstanceId");
            Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task DurableClient_ExternalApp_TerminateAsync_TerminateEventPlaced()
        {
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));

            var durableOrchestrationClient = this.GetDurableClient(orchestrationServiceClientMock.Object);
            await durableOrchestrationClient.TerminateAsync("valid_instance_id", "any reason");
            orchestrationServiceClientMock.Verify(x => x.ForceTerminateTaskOrchestrationAsync("valid_instance_id", "any reason"), Times.Once());
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DurableClient_ExternalApp_CreateCheckStatusResponse_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();

            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));

            var durableOrchestrationClient = this.GetDurableClient(orchestrationServiceClientMock.Object);
            Assert.ThrowsAny<InvalidOperationException>(() => durableOrchestrationClient.CreateCheckStatusResponse(new HttpRequestMessage(), "testInstanceId"));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DurableClient_ExternalApp_CreateHttpManagementPayload_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();

            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Running));

            var durableOrchestrationClient = this.GetDurableClient(orchestrationServiceClientMock.Object);
            Assert.ThrowsAny<InvalidOperationException>(() => durableOrchestrationClient.CreateHttpManagementPayload("testInstanceId"));
        }

        private (IDurableClient Client, TestLogger Logger) CreateClientWithCapturedLogs(
            IOrchestrationServiceClient serviceClient)
        {
            var logger = new TestLogger(this.output, category: "UnitTest");
            var options = new DurableTaskOptions { HubName = "TestTaskHub" };
            var traceHelper = new EndToEndTraceHelper(logger, traceReplayEvents: false);
            var storageProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                serviceClient,
                "test");
            var attribute = new DurableClientAttribute { TaskHub = "TestTaskHub" };
            var converter = new MessagePayloadDataConverter(new JsonSerializerSettings(), true);
            var client = new DurableClient(storageProvider, null, attribute, converter, traceHelper, options);
            return ((IDurableClient)client, logger);
        }

        private static object GetLoggedTargetInstanceId(TestLogger logger)
        {
            LogMessage message = Assert.Single(logger.LogMessages);
            KeyValuePair<string, object> property = Assert.Single(
                message.State,
                item => item.Key == "targetInstanceId");
            return property.Value;
        }

        private IDurableOrchestrationClient GetDurableClient(IOrchestrationServiceClient orchestrationServiceClientMockObject)
        {
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMockObject, "test");
            DurableClientOptions durableClientOptions = new DurableClientOptions
            {
                ConnectionName = "Storage",
                TaskHub = "TestTaskHub",
            };
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            DurableClientAttribute attribute = new DurableClientAttribute(durableClientOptions);
            MessagePayloadDataConverter messagePayloadDataConverter = new MessagePayloadDataConverter(new JsonSerializerSettings(), true);
            var traceHelper = new EndToEndTraceHelper(new NullLogger<EndToEndTraceHelper>(), durableTaskOptions.Tracing.TraceReplayEvents);

            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, null, attribute, messagePayloadDataConverter, traceHelper, durableTaskOptions);
            return durableOrchestrationClient;
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task HttpRequest_HttpRequestMessage_ClientMethods_Identical()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>(MockBehavior.Strict);
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Completed));
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();

            // Use ExtendedHttpApiHandler so that GetClient() returns our mock-backed client
            // instead of creating a new one via DurableTaskExtension (which would bypass the mock).
            var httpHandler = new ExtendedHttpApiHandler(new Mock<IDurableClient>(MockBehavior.Strict).Object);
            var durableOrchestrationClient = (IDurableClient)new DurableClient(storageProvider, durableExtension, httpHandler, new DurableClientAttribute { });
            httpHandler.InnerClient = durableOrchestrationClient;

            string sampleUrl = "https://samplesite.azurewebsites.net";
            string sampleId = Guid.NewGuid().ToString();

            var netFrameworkRequest = new HttpRequestMessage(HttpMethod.Get, sampleUrl);
            HttpRequest netCoreRequest = await ConvertHttpRequestMessageAsync(netFrameworkRequest);

            HttpResponseMessage netFrameworkResponse = durableOrchestrationClient.CreateCheckStatusResponse(netFrameworkRequest, sampleId);
            HttpResponseMessage netCoreResponse = (HttpResponseMessage)((ObjectResult)durableOrchestrationClient.CreateCheckStatusResponse(netCoreRequest, sampleId)).Value;
            await AssertHttpResponsesEqual(netFrameworkResponse, netCoreResponse);

            netFrameworkResponse = durableOrchestrationClient.CreateCheckStatusResponse(netFrameworkRequest, sampleId, returnInternalServerErrorOnFailure: true);
            netCoreResponse = (HttpResponseMessage)((ObjectResult)durableOrchestrationClient.CreateCheckStatusResponse(netCoreRequest, sampleId, returnInternalServerErrorOnFailure: true)).Value;
            await AssertHttpResponsesEqual(netFrameworkResponse, netCoreResponse);

            netFrameworkResponse = await durableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(netFrameworkRequest, sampleId);
            netCoreResponse = (HttpResponseMessage)((ObjectResult)await durableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(netCoreRequest, sampleId)).Value;
            await AssertHttpResponsesEqual(netFrameworkResponse, netCoreResponse);

            netFrameworkResponse = await durableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(netFrameworkRequest, sampleId, returnInternalServerErrorOnFailure: true);
            netCoreResponse = (HttpResponseMessage)((ObjectResult)await durableOrchestrationClient.WaitForCompletionOrCreateCheckStatusResponseAsync(netCoreRequest, sampleId, returnInternalServerErrorOnFailure: true)).Value;
            await AssertHttpResponsesEqual(netFrameworkResponse, netCoreResponse);
        }

        private static async Task<HttpRequest> ConvertHttpRequestMessageAsync(HttpRequestMessage req)
        {
            HttpContext context = new DefaultHttpContext();
            context.Request.Host = new HostString(req.RequestUri.Host);
            context.Request.Path = req.RequestUri.AbsolutePath;
            context.Request.Scheme = req.RequestUri.Scheme;
            context.Request.QueryString = new QueryString(req.RequestUri.Query);
            context.Request.Method = req.Method.ToString();
            if (req.Content != null)
            {
                context.Request.Body = await req.Content.ReadAsStreamAsync();
            }

            foreach (var header in req.Headers)
            {
                context.Request.Headers[header.Key] = new StringValues(header.Value.ToArray());
            }

            return context.Request;
        }

        private static async Task AssertHttpResponsesEqual(HttpResponseMessage response1, HttpResponseMessage response2)
        {
            Assert.Equal(response1.StatusCode, response2.StatusCode);
            string body1 = await response1.Content.ReadAsStringAsync();
            string body2 = await response2.Content.ReadAsStringAsync();
            Assert.Equal(body1, body2);
        }

        private static List<OrchestrationState> GetInvalidInstanceState()
        {
            return null;
        }

        private static List<OrchestrationState> GetInstanceState(OrchestrationStatus status)
        {
            return new List<OrchestrationState>()
            {
                new OrchestrationState()
                {
                    OrchestrationInstance = new OrchestrationInstance
                    {
                        InstanceId = "valid_instance_id",
                    },
                    OrchestrationStatus = status,
                },
            };
        }

        private static DurableTaskExtension GetDurableTaskConfig()
        {
            var options = new DurableTaskOptions();
            options.HubName = "DurableTaskHub";
            options.WebhookUriProviderOverride = () => new Uri("https://sampleurl.net");
            var wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var nameResolver = TestHelpers.GetTestNameResolver();
            var clientProviderFactory = new TestStorageServiceClientProviderFactory();
            var platformInformationService = TestHelpers.GetMockPlatformInformationService();
            var serviceFactory = new AzureStorageDurabilityProviderFactory(
                wrappedOptions,
                clientProviderFactory,
                nameResolver,
                NullLoggerFactory.Instance,
                platformInformationService);
            return new DurableTaskExtension(
                wrappedOptions,
                new LoggerFactory(),
                nameResolver,
                new[] { serviceFactory },
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: platformInformationService);
        }

        // wraps the durability provider class so we can configure the CheckStatusBeforeRaiseEvent property
        private class DurabilityProvider : Microsoft.Azure.WebJobs.Extensions.DurableTask.DurabilityProvider
        {
            private readonly bool checkStatusBeforeRaiseEvent;

            public DurabilityProvider(string storageProviderName, IOrchestrationService service, IOrchestrationServiceClient serviceClient, string connectionName, bool checkStatusBeforeRaiseEvent = true)
                : base(storageProviderName, service, serviceClient, connectionName)
            {
                this.checkStatusBeforeRaiseEvent = checkStatusBeforeRaiseEvent;
            }

            public override bool CheckStatusBeforeRaiseEvent => this.checkStatusBeforeRaiseEvent;
        }
    }
}
