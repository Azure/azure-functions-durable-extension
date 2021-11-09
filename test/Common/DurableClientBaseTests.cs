// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DurableTask.Core;
#if !FUNCTIONS_V1
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
#endif
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
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();
            var durableClient = (IDurableEntityClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            var entityId = new EntityId("test", entityKey);
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await durableClient.SignalEntityAsync(entityId, "test"));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RaiseEventAsync_InvalidInstanceId_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(GetInvalidInstanceState());
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await durableOrchestrationClient.RaiseEventAsync("invalid_instance_id", "anyEvent", new { message = "any message" }));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RaiseEventAsync_NonRunningFunction_ThrowsException()
        {
            var instanceId = Guid.NewGuid().ToString();
            var orchestrationServiceClientMock = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClientMock.Setup(x => x.GetOrchestrationStateAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(GetInstanceState(OrchestrationStatus.Completed));
            var storageProvider = new DurabilityProvider("test", new Mock<IOrchestrationService>().Object, orchestrationServiceClientMock.Object, "test");
            var durableExtension = GetDurableTaskConfig();
            var durableOrchestrationClient = (IDurableOrchestrationClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });

            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await durableOrchestrationClient.RaiseEventAsync("valid_instance_id", "anyEvent", new { message = "any message" }));
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

#if !FUNCTIONS_V1
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
        public async void DurableClient_ExternalApp_GetStatusAsync_ReturnsStatus()
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
        public async void DurableClient_ExternalApp_TerminateAsync_TerminateEventPlaced()
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
            var durableOrchestrationClient = (IDurableClient)new DurableClient(storageProvider, durableExtension, durableExtension.HttpApiHandler, new DurableClientAttribute { });
            var httpHandler = new ExtendedHttpApiHandler(new Mock<IDurableClient>(MockBehavior.Strict).Object);

            // This is super hacky, but required due to the circular dependency of ExtendedHttpApiHandler requiring IDurableClient and DurableClient requiring ExtendedHttpApiHandler
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
#endif

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
            var connectionStringResolver = new TestConnectionStringResolver();
            var platformInformationService = TestHelpers.GetMockPlatformInformationService();
            var serviceFactory = new AzureStorageDurabilityProviderFactory(
                wrappedOptions,
                connectionStringResolver,
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
    }
}
