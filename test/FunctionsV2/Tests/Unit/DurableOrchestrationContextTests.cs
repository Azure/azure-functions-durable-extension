// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationContextTests
    {
        private readonly ITestOutputHelper output;

        public DurableOrchestrationContextTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Reproduces https://github.com/Azure/azure-functions-durable-extension/issues/2111.
        /// A <see cref="Timeout.InfiniteTimeSpan"/> passed to the timeout overload of
        /// WaitForExternalEvent used to be added to the orchestrator's current time, scheduling a
        /// durable timer in the past that fired immediately and faulted the returned task with a
        /// <see cref="TimeoutException"/>. It must instead wait indefinitely and only complete when
        /// the matching external event is delivered.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForExternalEvent_InfiniteTimeout_WaitsForEventInsteadOfTimingOut()
        {
            DurableOrchestrationContext context = CreateContext();

            // WaitForExternalEvent asserts it runs on the orchestrator thread.
            bool originalIsOrchestratorThread = OrchestrationContext.IsOrchestratorThread;
            OrchestrationContext.IsOrchestratorThread = true;
            try
            {
                Task<string> waitTask = ((IDurableOrchestrationContext)context)
                    .WaitForExternalEvent<string>("Approval", Timeout.InfiniteTimeSpan, CancellationToken.None);

                // Before the fix this task was already faulted with a TimeoutException; with the fix
                // it stays pending until the event arrives.
                Assert.False(waitTask.IsCompleted);

                context.RaiseEvent("Approval", "\"approved\"");

                // Bound the wait so a future regression fails fast instead of hanging the test run.
                string result = await waitTask.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal("approved", result);
            }
            finally
            {
                OrchestrationContext.IsOrchestratorThread = originalIsOrchestratorThread;
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallSubOrchestratorAsync_LogsExplicitTargetInstanceId()
        {
            var loggerProvider = new TestLoggerProvider(this.output);
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            DurableOrchestrationContext context = CreateContext(loggerFactory, out DurableTaskExtension extension);
            extension.RegisterOrchestrator(
                new FunctionName("Child"),
                new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));

            var innerContext = new Mock<OrchestrationContext>();
            innerContext
                .Setup(inner => inner.CreateSubOrchestrationInstance<string>("Child", null, "child-id", null))
                .ReturnsAsync("done");
            context.InnerContext = innerContext.Object;
            context.InstanceId = "parent-id";

            bool originalIsOrchestratorThread = OrchestrationContext.IsOrchestratorThread;
            OrchestrationContext.IsOrchestratorThread = true;
            try
            {
                await context.CallDurableTaskFunctionAsync<string>(
                    "Child",
                    FunctionType.Orchestrator,
                    oneWay: false,
                    instanceId: "child-id",
                    operation: null,
                    retryOptions: null,
                    input: null,
                    scheduledTimeUtc: null);
            }
            finally
            {
                OrchestrationContext.IsOrchestratorThread = originalIsOrchestratorThread;
            }

            LogMessage message = Assert.Single(
                loggerProvider.GetAllLogMessages(),
                item => item.FormattedMessage.Contains("Child (Orchestrator)"));
            KeyValuePair<string, object> target = Assert.Single(
                message.State,
                item => item.Key == "targetInstanceId");
            Assert.Equal("child-id", target.Value);
        }

        private static DurableOrchestrationContext CreateContext()
        {
            return CreateContext(NullLoggerFactory.Instance, out _);
        }

        private static DurableOrchestrationContext CreateContext(
            ILoggerFactory loggerFactory,
            out DurableTaskExtension extension)
        {
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };

            extension = new DurableTaskExtension(
                new OptionsWrapper<DurableTaskOptions>(options),
                loggerFactory,
                TestHelpers.GetTestNameResolver(),
                [
                    new AzureStorageDurabilityProviderFactory(
                        new OptionsWrapper<DurableTaskOptions>(options),
                        new TestStorageServiceClientProviderFactory(),
                        TestHelpers.GetTestNameResolver(),
                        NullLoggerFactory.Instance,
                        TestHelpers.GetMockPlatformInformationService()),
                ],
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService());

            var durabilityProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "test");

            var context = new DurableOrchestrationContext(extension, durabilityProvider, "TestOrch")
            {
                // The infinite-timeout path never dereferences the inner context, but
                // ThrowIfInvalidAccess requires it to be non-null.
                InnerContext = new Mock<OrchestrationContext>().Object,
            };

            return context;
        }
    }
}
