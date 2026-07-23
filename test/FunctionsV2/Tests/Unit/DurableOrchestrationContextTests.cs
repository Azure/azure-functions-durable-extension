// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableOrchestrationContextTests
    {
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

                string result = await waitTask;
                Assert.Equal("approved", result);
            }
            finally
            {
                OrchestrationContext.IsOrchestratorThread = originalIsOrchestratorThread;
            }
        }

        private static DurableOrchestrationContext CreateContext()
        {
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };

            var extension = new DurableTaskExtension(
                new OptionsWrapper<DurableTaskOptions>(options),
                NullLoggerFactory.Instance,
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
