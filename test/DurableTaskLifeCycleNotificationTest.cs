// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskLifeCycleNotificationTest
    {
        private readonly ITestOutputHelper output;
        private readonly ILoggerFactory loggerFactory;
        private readonly TestLoggerProvider loggerProvider;
        private readonly bool useTestLogger;

        public DurableTaskLifeCycleNotificationTest(ITestOutputHelper output)
        {
            this.output = output;
            this.useTestLogger = true;

            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory = new LoggerFactory();

            if (this.useTestLogger)
            {
                this.loggerFactory.AddProvider(this.loggerProvider);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationStartAndCompleted(bool extendedSessionsEnabled)
        {
            var functionName = nameof(TestOrchestrations.SayHelloInline);
            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequestMessage, HttpResponseMessage> responseGenerator =
                    (HttpRequestMessage req) => req.CreateResponse(HttpStatusCode.OK, "{\"message\":\"OK!\"}");

                int callCount = 0;
                List<Action> eventGridRequestValidators = this.ConfigureEventGridMockHandler(
                    host,
                    functionName,
                    createdInstanceId,
                    eventGridKeyValue,
                    eventGridEndpoint,
                    responseGenerator,
                    handler: (JObject eventPayload) =>
                    {
                        dynamic o = eventPayload;
                        if (callCount == 0)
                        {
                            Assert.Equal("durable/orchestrator/Running", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Running", (string)o.data.runtimeStatus);
                        }
                        else if (callCount == 1)
                        {
                            Assert.Equal("durable/orchestrator/Completed", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Completed", (string)o.data.runtimeStatus);
                        }
                        else
                        {
                            Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                        }

                        callCount++;
                    });

                var client = await host.StartOrchestratorAsync(
                    functionName,
                    "World",
                    this.output,
                    createdInstanceId);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                // There should be one validator for each Event Grid request.
                // Each validator is a delegate with several Assert statements.
                Assert.NotEmpty(eventGridRequestValidators);
                foreach (Action validator in eventGridRequestValidators)
                {
                    validator.Invoke();
                }

                Assert.Equal(2, callCount);

                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationFailed(bool extendedSessionsEnabled)
        {
            var functionName = nameof(TestOrchestrations.ThrowOrchestrator);
            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationFailed),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequestMessage, HttpResponseMessage> responseGenerator =
                    (HttpRequestMessage req) => req.CreateResponse(HttpStatusCode.OK, "{\"message\":\"OK!\"}");

                int callCount = 0;
                List<Action> eventGridRequestValidators = this.ConfigureEventGridMockHandler(
                    host,
                    functionName,
                    createdInstanceId,
                    eventGridKeyValue,
                    eventGridEndpoint,
                    responseGenerator,
                    handler: (JObject eventPayload) =>
                    {
                        dynamic o = eventPayload;
                        if (callCount == 0)
                        {
                            Assert.Equal("durable/orchestrator/Running", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Running", (string)o.data.runtimeStatus);
                        }
                        else if (callCount == 1)
                        {
                            Assert.Equal("durable/orchestrator/Failed", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Failed", (string)o.data.runtimeStatus);
                        }
                        else
                        {
                            Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                        }

                        callCount++;
                    });

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(
                    functionName,
                    null,
                    this.output,
                    createdInstanceId);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains("Value cannot be null"));

                // There should be one validator for each Event Grid request.
                // Each validator is a delegate with several Assert statements.
                Assert.NotEmpty(eventGridRequestValidators);
                foreach (Action validator in eventGridRequestValidators)
                {
                    validator.Invoke();
                }

                Assert.Equal(2, callCount);
                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationTerminate(bool extendedSessionsEnabled)
        {
            // Using the counter orchestration because it will wait indefinitely for input.
            var functionName = nameof(TestOrchestrations.Counter);
            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationTerminate),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequestMessage, HttpResponseMessage> responseGenerator =
                    (HttpRequestMessage req) => req.CreateResponse(HttpStatusCode.OK, "{\"message\":\"OK!\"}");

                int callCount = 0;
                List<Action> eventGridRequestValidators = this.ConfigureEventGridMockHandler(
                    host,
                    functionName,
                    createdInstanceId,
                    eventGridKeyValue,
                    eventGridEndpoint,
                    responseGenerator,
                    handler: (JObject eventPayload) =>
                    {
                        dynamic o = eventPayload;
                        if (callCount == 0)
                        {
                            Assert.Equal("durable/orchestrator/Running", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Running", (string)o.data.runtimeStatus);
                        }
                        else if (callCount == 1)
                        {
                            Assert.Equal("durable/orchestrator/Terminated", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Terminated", (string)o.data.runtimeStatus);
                        }
                        else
                        {
                            Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                        }

                        callCount++;
                    });

                var client = await host.StartOrchestratorAsync(
                    functionName,
                    0,
                    this.output,
                    createdInstanceId);

                await client.WaitForStartupAsync(TimeSpan.FromSeconds(30), this.output);
                await client.TerminateAsync("sayōnara");

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Terminated, status?.RuntimeStatus);
                Assert.Equal("sayōnara", status?.Output);

                // There should be one validator for each Event Grid request.
                // Each validator is a delegate with several Assert statements.
                Assert.NotEmpty(eventGridRequestValidators);
                foreach (Action validator in eventGridRequestValidators)
                {
                    validator.Invoke();
                }

                // TODO: There should be two calls, but the termination notification is not being fired.
                //       https://github.com/Azure/azure-functions-durable-extension/issues/286
                Assert.Equal(1, callCount);
                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationEventGridApiReturnBadStatus(bool extendedSessionsEnabled)
        {
            var functionName = nameof(TestOrchestrations.SayHelloInline);
            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequestMessage, HttpResponseMessage> responseGenerator =
                    (HttpRequestMessage req) => req.CreateResponse(
                        HttpStatusCode.InternalServerError,
                        new { message = "Exception has been thrown" });

                int callCount = 0;
                List<Action> eventGridRequestValidators = this.ConfigureEventGridMockHandler(
                    host,
                    functionName,
                    createdInstanceId,
                    eventGridKeyValue,
                    eventGridEndpoint,
                    responseGenerator,
                    handler: (JObject eventPayload) =>
                    {
                        dynamic o = eventPayload;
                        if (callCount == 0)
                        {
                            Assert.Equal("durable/orchestrator/Running", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Running", (string)o.data.runtimeStatus);
                        }
                        else if (callCount == 1)
                        {
                            Assert.Equal("durable/orchestrator/Completed", (string)o.subject);
                            Assert.Equal("orchestratorEvent", (string)o.eventType);
                            Assert.Equal("Completed", (string)o.data.runtimeStatus);
                        }
                        else
                        {
                            Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                        }

                        callCount++;
                    });

                var client = await host.StartOrchestratorAsync(
                    functionName,
                    "World",
                    this.output,
                    createdInstanceId);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                // There should be one validator for each Event Grid request.
                // Each validator is a delegate with several Assert statements.
                Assert.NotEmpty(eventGridRequestValidators);
                foreach (Action validator in eventGridRequestValidators)
                {
                    validator.Invoke();
                }

                Assert.Equal(2, callCount);

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "OrchestrationEventGridApiReturnBadStatus",
                        client.InstanceId,
                        extendedSessionsEnabled,
                        new[] { functionName });
                }

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task ConfigurationWihtoutEventGridKeySettingName()
        {
            string eventGridKeyValue = null;
            var eventGridKeySettingName = "";
            var eventGridEndpoint = "http://dymmy.com/";
            var mockNameResolver = GetNameResolverMock(Array.Empty<(string, string)>());

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationTerminate),
                false /* extendedSessionsEnabled */,
                eventGridKeySettingName,
                mockNameResolver.Object,
                eventGridEndpoint))
            {
                var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await host.StartAsync());
                Assert.Equal($"Failed to start lifecycle notification feature. Please check the configuration values for {eventGridEndpoint} and {eventGridKeySettingName}.", ex.Message);
            }
        }

        [Fact]
        public async Task ConfigurationWithoutEventGridKeyValue()
        {
            string eventGridKeyValue = null;
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var mockNameResolver = GetNameResolverMock(Array.Empty<(string, string)>());
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationTerminate),
                false /* extendedSessionsEnabled */,
                eventGridKeySettingName,
                mockNameResolver.Object,
                eventGridEndpoint))
            {
                var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await host.StartAsync());
                Assert.Equal($"Failed to start lifecycle notification feature. Please check the configuration values for {eventGridKeySettingName} on AppSettings.", ex.Message);
            }
        }

        private List<Action> ConfigureEventGridMockHandler(
            JobHost host,
            string functionName,
            string createdInstanceId,
            string eventGridKeyValue,
            string eventGridEndpoint,
            Func<HttpRequestMessage, HttpResponseMessage> responseGenerator,
            Action<JObject> handler)
        {
            var extensionRegistry = (IExtensionRegistry)host.Services.GetService(typeof(IExtensionRegistry));
            var extensionProviders = extensionRegistry.GetExtensions(typeof(IExtensionConfigProvider))
                .Where(x => x is DurableTaskExtension)
                .ToList();

            var assertBodies = new List<Action>();
            var extension = (DurableTaskExtension)extensionProviders.First();
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
                {
                    // We can't assert here directly because any unhandled exceptions will cause
                    // DTFx to abort the work item, which would make debugging failures extremely
                    // difficult. Instead, we capture the asserts in a set of lambda expressions
                    // which can be invoked on the main test thread later.
                    assertBodies.Add(() =>
                    {
                        Assert.Contains(request.Headers, x => x.Key == "aeg-sas-key");
                        var values = request.Headers.GetValues("aeg-sas-key").ToList();
                        Assert.Single(values);
                        Assert.Equal(eventGridKeyValue, values[0]);
                        Assert.Equal(eventGridEndpoint, request.RequestUri.ToString());
                        var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        this.output.WriteLine("Event Grid notification: " + json);

                        JArray content = JArray.Parse(json);
                        dynamic o = (JObject)Assert.Single(content);

                        string instanceId = o.data.instanceId;
                        Assert.NotNull(instanceId);
                        if (instanceId != createdInstanceId)
                        {
                            // This might be from a previous or concurrent run
                            return;
                        }

                        Assert.Equal("1.0", o.dataVersion.ToString());
                        Assert.Equal(extension.HubName, o.data.hubName.ToString());
                        Assert.Equal(functionName, o.data.functionName.ToString());

                        handler(o);
                    });

                    return Task.FromResult(responseGenerator(request));
                });

            extension.LifeCycleNotificationHelper.HttpMessageHandler = mock.Object;

            return assertBodies;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationEventGridApiServiceUnavailableRetry(bool extendedSessionsEnabled)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var callCount = 0;
            var retryCount = 5;
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted), extendedSessionsEnabled, eventGridKeySettingName, mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();
                var extensionRegistry = (IExtensionRegistry)host.Services.GetService(typeof(IExtensionRegistry));
                var extensionProviders = extensionRegistry.GetExtensions(typeof(IExtensionConfigProvider))
                    .Where(x => x is DurableTaskExtension)
                    .ToList();

                if (extensionProviders.Any())
                {
                    var extension = (DurableTaskExtension)extensionProviders.First();
                    var mock = new Mock<HttpMessageHandler>();
                    mock.Protected()
                        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                        .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
                        {
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            JArray content = JArray.Parse(json);
                            dynamic o = (JObject)Assert.Single(content);
                            if (o.subject.ToString() == "durable/orchestrator/Running")
                            {
                                callCount++;
                                if (callCount > retryCount)
                                {
                                    var message = new HttpResponseMessage(HttpStatusCode.OK);
                                    message.Content = new StringContent("{\"message\":\"OK!\"}");
                                    return Task.FromResult(message);
                                }
                                else
                                {
                                    var message = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                                    message.Content = new StringContent("{\"message\":\"Exception has been thrown\"}");
                                    return Task.FromResult(message);
                                }
                            }
                            else if (o.subject.ToString() == "durable/orchestrator/Completed")
                            {
                                var message = new HttpResponseMessage(HttpStatusCode.OK);
                                message.Content = new StringContent("{\"message\":\"OK!\"}");
                                return Task.FromResult(message);
                            }
                            throw new Exception("subject is fault type");
                        });

                    extension.LifeCycleNotificationHelper.HttpMessageHandler =
                        new LifeCycleNotificationHelper.HttpRetryMessageHandler(
                            mock.Object,
                            5,
                            TimeSpan.FromMilliseconds(1000),
                            Array.Empty<HttpStatusCode>());
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);
                Assert.Equal(retryCount + 1, callCount);
                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationEventGridApiExceptionRetry(bool extendedSessionsEnabled)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var callCount = 0;
            var retryCount = 5;
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted), extendedSessionsEnabled, eventGridKeySettingName, mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();
                var extensionRegistry = (IExtensionRegistry)host.Services.GetService(typeof(IExtensionRegistry));
                var extensionProviders = extensionRegistry.GetExtensions(typeof(IExtensionConfigProvider))
                    .Where(x => x is DurableTaskExtension)
                    .ToList();

                if (extensionProviders.Any())
                {
                    var extension = (DurableTaskExtension)extensionProviders.First();
                    var mock = new Mock<HttpMessageHandler>();
                    mock.Protected()
                        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                        .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
                        {
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            JArray content = JArray.Parse(json);
                            dynamic o = (JObject)Assert.Single(content);

                            if (o.subject.ToString() == "durable/orchestrator/Running")
                            {
                                callCount++;
                                if (callCount > retryCount)
                                {
                                    var message = new HttpResponseMessage(HttpStatusCode.OK);
                                    message.Content = new StringContent("{\"message\":\"OK!\"}");
                                    return Task.FromResult(message);
                                }
                                else
                                {
                                    throw new HttpRequestException();
                                }
                            }
                            else if (o.subject.ToString() == "durable/orchestrator/Completed")
                            {
                                var message = new HttpResponseMessage(HttpStatusCode.OK);
                                message.Content = new StringContent("{\"message\":\"OK!\"}");
                                return Task.FromResult(message);
                            }

                            throw new Exception("subject is fault type");
                        });

                    extension.LifeCycleNotificationHelper.HttpMessageHandler =
                        new LifeCycleNotificationHelper.HttpRetryMessageHandler(
                            mock.Object,
                            5,
                            TimeSpan.FromMilliseconds(1000),
                            Array.Empty<HttpStatusCode>());
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);
                Assert.Equal(retryCount + 1, callCount);
                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationEventGridApiExceptionNoRetry(bool extendedSessionsEnabled)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var callCount = 0;
            var retryCount = 0;
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted), extendedSessionsEnabled, eventGridKeySettingName, mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();
                var extensionRegistry = (IExtensionRegistry)host.Services.GetService(typeof(IExtensionRegistry));
                var extensionProviders = extensionRegistry.GetExtensions(typeof(IExtensionConfigProvider))
                    .Where(x => x is DurableTaskExtension)
                    .ToList();

                if (extensionProviders.Any())
                {
                    var extension = (DurableTaskExtension)extensionProviders.First();
                    var mock = new Mock<HttpMessageHandler>();
                    mock.Protected()
                        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                        .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
                        {
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            JArray content = JArray.Parse(json);
                            dynamic o = (JObject)Assert.Single(content);

                            if (o.subject.ToString() == "durable/orchestrator/Running")
                            {
                                callCount++;
                                throw new HttpRequestException();
                            }
                            else if (o.subject.ToString() == "durable/orchestrator/Completed")
                            {
                                var message = new HttpResponseMessage(HttpStatusCode.OK);
                                message.Content = new StringContent("{\"message\":\"OK!\"}");
                                return Task.FromResult(message);
                            }

                            throw new Exception("subject is fault type");
                        });

                    extension.LifeCycleNotificationHelper.HttpMessageHandler =
                        new LifeCycleNotificationHelper.HttpRetryMessageHandler(
                            mock.Object,
                            retryCount,
                            TimeSpan.FromMilliseconds(1000),
                            Array.Empty<HttpStatusCode>());
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(300), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);
                Assert.Equal(retryCount + 1, callCount);
                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationEventGridApiExceptionRetryCountOver(bool extendedSessionsEnabled)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var callCount = 0;
            var retryCount = 5;
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted), extendedSessionsEnabled, eventGridKeySettingName, mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();
                var extensionRegistry = (IExtensionRegistry)host.Services.GetService(typeof(IExtensionRegistry));
                var extensionProviders = extensionRegistry.GetExtensions(typeof(IExtensionConfigProvider))
                    .Where(x => x is DurableTaskExtension)
                    .ToList();

                if (extensionProviders.Any())
                {
                    var extension = (DurableTaskExtension)extensionProviders.First();
                    var mock = new Mock<HttpMessageHandler>();
                    mock.Protected()
                        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                        .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
                        {
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            JArray content = JArray.Parse(json);
                            dynamic o = (JObject)Assert.Single(content);

                            if (o.subject.ToString() == "durable/orchestrator/Running")
                            {
                                callCount++;
                                throw new HttpRequestException();
                            }
                            else if (o.subject.ToString() == "durable/orchestrator/Completed")
                            {
                                var message = new HttpResponseMessage(HttpStatusCode.OK);
                                message.Content = new StringContent("{\"message\":\"OK!\"}");
                                return Task.FromResult(message);
                            }

                            throw new Exception("subject is fault type");
                        });

                    extension.LifeCycleNotificationHelper.HttpMessageHandler =
                        new LifeCycleNotificationHelper.HttpRetryMessageHandler(
                            mock.Object,
                            5,
                            TimeSpan.FromMilliseconds(1000),
                            Array.Empty<HttpStatusCode>());
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(300), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);
                Assert.Equal(retryCount + 1, callCount);
                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationEventGridApiRetryStatus(bool extendedSessionsEnabled)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            var callCount = 0;
            var retryCount = 5;
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue) });

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted), extendedSessionsEnabled, eventGridKeySettingName, mockNameResolver.Object,
                eventGridEndpoint))
            {
                await host.StartAsync();
                var extensionRegistry = (IExtensionRegistry)host.Services.GetService(typeof(IExtensionRegistry));
                var extensionProviders = extensionRegistry.GetExtensions(typeof(IExtensionConfigProvider))
                    .Where(x => x is DurableTaskExtension)
                    .ToList();

                if (extensionProviders.Any())
                {
                    var extension = (DurableTaskExtension)extensionProviders.First();
                    var mock = new Mock<HttpMessageHandler>();
                    mock.Protected()
                        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                        .Returns((HttpRequestMessage request, CancellationToken cancellationToken) =>
                        {
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            JArray content = JArray.Parse(json);
                            dynamic o = (JObject)Assert.Single(content);

                            if (o.subject.ToString() == "durable/orchestrator/Running")
                            {
                                callCount++;
                                HttpResponseMessage message = null;
                                if (callCount == 1)
                                {
                                    message = new HttpResponseMessage(HttpStatusCode.BadRequest);
                                }
                                else if (callCount == 2)
                                {
                                    message = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                                }
                                else if (callCount == 3)
                                {
                                    message = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                                }
                                else if (callCount == 4)
                                {
                                    message = new HttpResponseMessage(HttpStatusCode.NotFound);
                                }
                                else
                                {
                                    message = new HttpResponseMessage(HttpStatusCode.OK);
                                    message.Content = new StringContent("{\"message\":\"OK!\"}");
                                    return Task.FromResult(message);
                                }

                                message.Content = new StringContent("{\"message\":\"Exception has been thrown\"}");
                                return Task.FromResult(message);
                            }
                            else if (o.subject.ToString() == "durable/orchestrator/Completed")
                            {
                                var message = new HttpResponseMessage(HttpStatusCode.OK);
                                message.Content = new StringContent("{\"message\":\"OK!\"}");
                                return Task.FromResult(message);
                            }

                            throw new Exception("subject is fault type");
                        });

                    extension.LifeCycleNotificationHelper.HttpMessageHandler =
                        new LifeCycleNotificationHelper.HttpRetryMessageHandler(
                            mock.Object,
                            5,
                            TimeSpan.FromMilliseconds(1000),
                            new[] { (HttpStatusCode)400, (HttpStatusCode)401, (HttpStatusCode)404 });
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);
                Assert.Equal(5, callCount);
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task OrchestrationEventGridApiConfigureCheck()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKeyValue = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://%WholeStringTest%.com/";
            var retryCount = 5;
            var retryInterval = TimeSpan.FromSeconds(10);
            var retryStatus = new[] { 400, 401 };
            var mockNameResolver = GetNameResolverMock(new[] { (eventGridKeySettingName, eventGridKeyValue), ("WholeStringTest", "dummy") });

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted), 
                false,
                eventGridKeySettingName,
                mockNameResolver.Object,
                eventGridEndpoint,
                retryCount,
                retryInterval,
                retryStatus))
            {
                await host.StartAsync();
                var extensionRegistry = (IExtensionRegistry)host.Services.GetService(typeof(IExtensionRegistry));
                var extensionProviders = extensionRegistry.GetExtensions(typeof(IExtensionConfigProvider))
                    .Where(x => x is DurableTaskExtension)
                    .ToList();
                var extension = (DurableTaskExtension)extensionProviders.First();

                Assert.Equal("http://dummy.com/", extension.LifeCycleNotificationHelper.EventGridTopicEndpoint);
                Assert.Equal(eventGridKeyValue, extension.LifeCycleNotificationHelper.EventGridKeyValue);

                var handler =
                    (LifeCycleNotificationHelper.HttpRetryMessageHandler) extension.LifeCycleNotificationHelper
                        .HttpMessageHandler;

                Assert.Equal(retryCount, handler.MaxRetryCount);
                Assert.Equal(retryInterval, handler.RetryWaitSpan);
                Assert.Equal(retryStatus.Select(x => (HttpStatusCode)x), handler.RetryTargetStatus);
                await host.StopAsync();
            }
        }

        private static Mock<INameResolver> GetNameResolverMock((string Key, string Value)[] settings)
        {
            var mock = new Mock<INameResolver>();
            foreach (var setting in settings)
            {
                mock.Setup(x => x.Resolve(setting.Key)).Returns(setting.Value);
            }

            return mock;
        }
    }
}
