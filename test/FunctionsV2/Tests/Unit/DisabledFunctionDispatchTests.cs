// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Tests for how the DTFx activity object manager (<see cref="DurableTaskExtension"/>'s
    /// <see cref="INameVersionObjectManager{T}.GetObject"/>) handles activities that are registered
    /// but not runnable — specifically disabled-but-still-deployed functions, which are indexed with a
    /// null executor. See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
    /// </summary>
    public class DisabledFunctionDispatchTests
    {
        private static readonly TaskContext TestTaskContext =
            new TaskContext(new OrchestrationInstance { InstanceId = "test-instance", ExecutionId = "test-execution" });

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetObject_DisabledActivity_ReturnsShimThatFailsAsDisabled()
        {
            // Arrange: simulate indexing of a disabled-but-deployed activity. The binding provider
            // registers the name during indexing with a null executor, and because the function is
            // disabled its listener never runs to replace that null. So knownActivities contains the
            // entry with Executor == null.
            var extension = CreateExtension();
            extension.RegisterActivity(new FunctionName("DisabledActivity"), executor: null!);

            var objectManager = (INameVersionObjectManager<TaskActivity>)extension;

            // Act: GetObject must NOT throw ArgumentNullException (the pre-fix poison-loop behavior).
            var shim = objectManager.GetObject("DisabledActivity", version: string.Empty);

            // Assert: we get a shim that fails deterministically with a "disabled" message.
            var disabledShim = Assert.IsType<TaskNonexistentActivityShim>(shim);

            TaskFailureException failure = Assert.Throws<TaskFailureException>(
                () => disabledShim.Run(TestTaskContext, "null"));
            Assert.Contains("DisabledActivity", failure.Message);
            Assert.Contains("is disabled", failure.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetObject_NonexistentActivity_ReturnsShimThatFailsAsNonexistent()
        {
            // Arrange: an activity that was never registered at all (e.g. deleted/renamed).
            var extension = CreateExtension();

            var objectManager = (INameVersionObjectManager<TaskActivity>)extension;

            // Act
            var shim = objectManager.GetObject("GhostActivity", version: string.Empty);

            // Assert: still a graceful, deterministic failure — but with a "does not exist" message.
            var nonexistentShim = Assert.IsType<TaskNonexistentActivityShim>(shim);

            TaskFailureException failure = Assert.Throws<TaskFailureException>(
                () => nonexistentShim.Run(TestTaskContext, "null"));
            Assert.Contains("GhostActivity", failure.Message);
            Assert.Contains("does not exist", failure.Message);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetObject_ActiveActivity_ReturnsRealActivityShim()
        {
            // Arrange: a normally registered activity with an active listener/executor.
            var extension = CreateExtension();
            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            extension.RegisterActivity(new FunctionName("ActiveActivity"), mockExecutor.Object);

            var objectManager = (INameVersionObjectManager<TaskActivity>)extension;

            // Act
            var shim = objectManager.GetObject("ActiveActivity", version: string.Empty);

            // Assert: the real activity shim (which delegates to the executor) is returned.
            Assert.IsType<TaskActivityShim>(shim);
        }

        private static DurableTaskExtension CreateExtension()
        {
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };

            return new DurableTaskExtension(
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
        }
    }
}
