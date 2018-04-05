// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests
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

        [Fact]
        public async Task OrchestrationStartAndCompleted()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKey = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            Environment.SetEnvironmentVariable(eventGridKeySettingName, eventGridKey);
            var callCount = 0;

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.OrchestrationStartAndCompleted), eventGridKeySettingName, eventGridEndpoint))
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
                                Assert.True(request.Headers.Any(x => x.Key == "aeg-sas-key"));
                                var values = request.Headers.GetValues("aeg-sas-key").ToList();
                                Assert.Single(values);
                                Assert.Equal(eventGridKey, values[0]);
                                Assert.Equal(eventGridEndpoint, request.RequestUri.ToString());
                                var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                                dynamic content = JsonConvert.DeserializeObject(json);
                                foreach (dynamic o in content)
                                {
                                    Assert.Equal("1.0", o.dataVersion.ToString());
                                    Assert.Equal(nameof(this.OrchestrationStartAndCompleted), o.data.HubName.ToString());
                                    Assert.Equal(orchestratorFunctionNames[0], o.data.FunctionName.ToString());

                                    if (callCount == 0)
                                    {
                                        Assert.Equal("durable/orchestrator/Running", o.subject.ToString());
                                        Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                        Assert.Equal("0", o.data.EventType.ToString());
                                    }
                                    else if (callCount == 1)
                                    {
                                        Assert.Equal("durable/orchestrator/Completed", o.subject.ToString());
                                        Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                        Assert.Equal("1", o.data.EventType.ToString());
                                    }
                                    else
                                    {
                                        Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                                    }
                                }

                                callCount++;
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                            });

                    extension.LifeCycleNotificationHelper.SetHttpMessageHandler(mock.Object);
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);
                Assert.Equal(2, callCount);

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task OrchestrationFailed()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Throw),
            };

            var eventGridKey = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            Environment.SetEnvironmentVariable(eventGridKeySettingName, eventGridKey);
            var callCount = 0;

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.OrchestrationFailed), eventGridKeySettingName, eventGridEndpoint))
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
                            Assert.True(request.Headers.Any(x => x.Key == "aeg-sas-key"));
                            var values = request.Headers.GetValues("aeg-sas-key").ToList();
                            Assert.Single(values);
                            Assert.Equal(eventGridKey, values[0]);
                            Assert.Equal(eventGridEndpoint, request.RequestUri.ToString());
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            dynamic content = JsonConvert.DeserializeObject(json);
                            foreach (dynamic o in content)
                            {
                                Assert.Equal("1.0", o.dataVersion.ToString());
                                Assert.Equal(nameof(this.OrchestrationFailed), o.data.HubName.ToString());
                                Assert.Equal(orchestratorFunctionNames[0], o.data.FunctionName.ToString());

                                if (callCount == 0)
                                {
                                    Assert.Equal("durable/orchestrator/Running", o.subject.ToString());
                                    Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                    Assert.Equal("0", o.data.EventType.ToString());
                                }
                                else if (callCount == 1)
                                {
                                    Assert.Equal("durable/orchestrator/Failed", o.subject.ToString());
                                    Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                    Assert.Equal("3", o.data.EventType.ToString());
                                }
                                else
                                {
                                    Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                                }
                            }

                            callCount++;
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        });

                    extension.LifeCycleNotificationHelper.SetHttpMessageHandler(mock.Object);
                }

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains("Value cannot be null"));

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task OrchestrationTerminate()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Counter),
            };
            var eventGridKey = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            Environment.SetEnvironmentVariable(eventGridKeySettingName, eventGridKey);
            var callCount = 0;

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.OrchestrationTerminate), eventGridKeySettingName, eventGridEndpoint))
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
                            Assert.True(request.Headers.Any(x => x.Key == "aeg-sas-key"));
                            var values = request.Headers.GetValues("aeg-sas-key").ToList();
                            Assert.Single(values);
                            Assert.Equal(eventGridKey, values[0]);
                            Assert.Equal(eventGridEndpoint, request.RequestUri.ToString());
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            dynamic content = JsonConvert.DeserializeObject(json);
                            foreach (dynamic o in content)
                            {
                                Assert.Equal("1.0", o.dataVersion.ToString());
                                Assert.Equal(nameof(this.OrchestrationTerminate), o.data.HubName.ToString());
                                Assert.Equal(orchestratorFunctionNames[0], o.data.FunctionName.ToString());

                                if (callCount == 0)
                                {
                                    Assert.Equal("durable/orchestrator/Running", o.subject.ToString());
                                    Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                    Assert.Equal("0", o.data.EventType.ToString());
                                }
                                else if (callCount == 1)
                                {
                                    Assert.Equal("durable/orchestrator/Terminated", o.subject.ToString());
                                    Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                    Assert.Equal("5", o.data.EventType.ToString());
                                }
                                else
                                {
                                    Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                                }
                            }

                            callCount++;
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        });

                    extension.LifeCycleNotificationHelper.SetHttpMessageHandler(mock.Object);
                }

                // Using the counter orchestration because it will wait indefinitely for input.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], 0, this.output);

                // Need to wait for the instance to start before we can terminate it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(30), this.output);

                await client.TerminateAsync("sayōnara");

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Terminated, status?.RuntimeStatus);
                Assert.Equal("sayōnara", status?.Output);

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task OrchestrationEventGridApiReturnBadStatus()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            var eventGridKey = "testEventGridKey";
            var eventGridKeySettingName = "eventGridKeySettingName";
            var eventGridEndpoint = "http://dymmy.com/";
            Environment.SetEnvironmentVariable(eventGridKeySettingName, eventGridKey);
            var callCount = 0;

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.OrchestrationStartAndCompleted), eventGridKeySettingName, eventGridEndpoint))
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
                            Assert.True(request.Headers.Any(x => x.Key == "aeg-sas-key"));
                            var values = request.Headers.GetValues("aeg-sas-key").ToList();
                            Assert.Single(values);
                            Assert.Equal(eventGridKey, values[0]);
                            Assert.Equal(eventGridEndpoint, request.RequestUri.ToString());
                            var json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            dynamic content = JsonConvert.DeserializeObject(json);
                            foreach (dynamic o in content)
                            {
                                Assert.Equal("1.0", o.dataVersion.ToString());
                                Assert.Equal(nameof(this.OrchestrationStartAndCompleted), o.data.HubName.ToString());
                                Assert.Equal(orchestratorFunctionNames[0], o.data.FunctionName.ToString());

                                if (callCount == 0)
                                {
                                    Assert.Equal("durable/orchestrator/Running", o.subject.ToString());
                                    Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                    Assert.Equal("0", o.data.EventType.ToString());
                                }
                                else if (callCount == 1)
                                {
                                    Assert.Equal("durable/orchestrator/Completed", o.subject.ToString());
                                    Assert.Equal("orchestratorEvent", o.eventType.ToString());
                                    Assert.Equal("1", o.data.EventType.ToString());
                                }
                                else
                                {
                                    Assert.True(false, "The calls to Event Grid should be exactly 2 but we are registering more.");
                                }
                            }

                            callCount++;
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                        });

                    extension.LifeCycleNotificationHelper.SetHttpMessageHandler(mock.Object);
                }

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);
                Assert.Equal(2, callCount);

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "OrchestrationEventGridApiReturnBadStatus",
                        orchestratorFunctionNames);
                }

                await host.StopAsync();
            }
        }
    }
}
