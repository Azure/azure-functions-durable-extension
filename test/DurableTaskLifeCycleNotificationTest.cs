// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests;
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

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                eventGridKeyValue,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequest, Task<HttpResponse>> responseGenerator =
                    HttpTestUtility.GetSimpleResponse;

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

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationFailed),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                eventGridKeyValue,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequest, Task<HttpResponse>> responseGenerator =
                    (HttpRequest req) => req.CreateResponse(HttpStatusCode.OK, "{\"message\":\"OK!\"}");

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

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationTerminate),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                eventGridKeyValue,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequest, Task<HttpResponse>> responseGenerator =
                    HttpTestUtility.GetSimpleResponse;

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

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationStartAndCompleted),
                extendedSessionsEnabled,
                eventGridKeySettingName,
                eventGridKeyValue,
                eventGridEndpoint))
            {
                await host.StartAsync();

                string createdInstanceId = Guid.NewGuid().ToString("N");

                Func<HttpRequest, Task<HttpResponse>> responseGenerator =
                    HttpTestUtility.GetSimpleErrorResponse;

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
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationTerminate),
                false /* extendedSessionsEnabled */,
                eventGridKeySettingName,
                eventGridKeyValue,
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
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerFactory,
                nameof(this.OrchestrationTerminate),
                false /* extendedSessionsEnabled */,
                eventGridKeySettingName,
                eventGridKeyValue,
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
            Func<HttpRequest, Task<HttpResponse>> responseGenerator,
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
                .Setup<Task<HttpResponse>>("SendAsync", ItExpr.IsAny<HttpRequest>(), ItExpr.IsAny<CancellationToken>())
                .Returns((HttpRequest request, CancellationToken cancellationToken) =>
                {
                    // We can't assert here directly because any unhandled exceptions will cause
                    // DTFx to abort the work item, which would make debugging failures extremely
                    // difficult. Instead, we capture the asserts in a set of lambda expressions
                    // which can be invoked on the main test thread later.
                    assertBodies.Add(() =>
                    {
                        Assert.Contains(request.Headers, x => x.Key == "aeg-sas-key");
                        var values = request.Headers["aeg-sas-key"].ToList();
                        Assert.Single(values);
                        Assert.Equal(eventGridKeyValue, values[0]);
                        Assert.Equal(eventGridEndpoint, request.Path.ToString());
                        var json = HttpTestUtility.GetRequestBody(request).GetAwaiter().GetResult();
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

                    return responseGenerator(request);
                });

            extension.LifeCycleNotificationHelper.SetHttpMessageHandler(mock.Object);

            return assertBodies;
        }
    }
}
