// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Listener;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using DTCore = DurableTask.Core;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class ExternalActivitySchedulingTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DurabilityProvider_DisallowsExternalActivitySchedulingByDefault()
        {
            var provider = new DurabilityProvider(
                "TestProvider",
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "TestConnection");

            Assert.False(provider.AllowsExternalActivityScheduling);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(null, true)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivity_UnknownActivityIsRejectedByDefaultOrWhenPolicyIsOff(
            bool? allowsExternalActivityScheduling, bool withRetry)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling);
            DurableOrchestrationContext context = CreateContext(provider.Object, out _, out var innerContext);

            ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(
                () => CallActivityAsync(context, "RemoteActivity", "input", withRetry));

            Assert.Contains("RemoteActivity", error.Message);
            Assert.Contains("not an activity function", error.Message);
            innerContext.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivity_ProviderPolicyAllowsUnregisteredActivity(bool withRetry)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling: true);
            DurableOrchestrationContext context = CreateContext(provider.Object, out var extension, out var innerContext);
            var input = new { Value = 42 };
            innerContext
                .Setup(inner => inner.ScheduleTask<string>("RemoteActivity", string.Empty, It.IsAny<object[]>()))
                .ReturnsAsync("remote-result");

            string result = await CallActivityAsync(context, "RemoteActivity", input, withRetry);

            Assert.Equal("remote-result", result);
            innerContext.Verify(
                inner => inner.ScheduleTask<string>(
                    "RemoteActivity",
                    string.Empty,
                    It.Is<object[]>(args => args.Length == 1 && ReferenceEquals(args[0], input))),
                Times.Once);
            if (withRetry)
            {
                innerContext.Verify(
                    inner => inner.ScheduleWithRetry<string>(
                        "RemoteActivity",
                        string.Empty,
                        It.Is<DTCore.RetryOptions>(options =>
                            options.FirstRetryInterval == TimeSpan.FromSeconds(1) &&
                            options.MaxNumberOfAttempts == 3),
                        It.Is<object[]>(args => args.Length == 1 && ReferenceEquals(args[0], input))),
                    Times.Once);
            }

            Assert.Empty(extension.GetActiveRegisteredFunctionNames().activityNames);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivity_RegisteredActivityStillSchedules(bool allowsExternalActivityScheduling, bool withRetry)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling);
            DurableOrchestrationContext context = CreateContext(provider.Object, out var extension, out var innerContext);
            extension.RegisterActivity(new FunctionName("LocalActivity"), new Mock<ITriggeredFunctionExecutor>().Object);
            innerContext
                .Setup(inner => inner.ScheduleTask<string>("LocalActivity", string.Empty, It.IsAny<object[]>()))
                .ReturnsAsync("local-result");

            string result = await CallActivityAsync(context, "LocalActivity", null, withRetry);

            Assert.Equal("local-result", result);
            innerContext.Verify(
                inner => inner.ScheduleTask<string>(
                    "LocalActivity",
                    string.Empty,
                    It.Is<object[]>(args => args.Length == 1 && args[0] == null)),
                Times.Once);
        }

        [Theory]
        [InlineData(null, false, false)]
        [InlineData(null, false, true)]
        [InlineData(null, true, false)]
        [InlineData(null, true, true)]
        [InlineData("", false, false)]
        [InlineData("", false, true)]
        [InlineData("", true, false)]
        [InlineData("", true, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivity_InvalidNameDoesNotScheduleAnyAction(
            string? name, bool allowsExternalActivityScheduling, bool withRetry)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling);
            DurableOrchestrationContext context = CreateContext(provider.Object, out _, out var innerContext);

            ArgumentException error = await Assert.ThrowsAnyAsync<ArgumentException>(
                () => CallActivityAsync(context, name, null, withRetry));

            if (name == null)
            {
                Assert.IsType<ArgumentNullException>(error);
            }

            innerContext.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivity_ProviderPolicyDoesNotBypassActionLimit(bool withRetry)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling: true);
            DurableOrchestrationContext context = CreateContext(
                provider.Object, out _, out var innerContext, maxActions: 1);
            innerContext
                .Setup(inner => inner.ScheduleTask<string>("RemoteActivity", string.Empty, It.IsAny<object[]>()))
                .ReturnsAsync("result");

            await CallActivityAsync(context, "RemoteActivity", null, withRetry);
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CallActivityAsync(context, "RemoteActivity", null, withRetry));

            Assert.Contains("Maximum amount of orchestration actions (1)", error.Message);
            innerContext.Verify(
                inner => inner.ScheduleTask<string>("RemoteActivity", string.Empty, It.IsAny<object[]>()),
                Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivity_ProviderPolicyDoesNotBypassThreadGuard(bool withRetry)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling: true);
            DurableOrchestrationContext context = CreateContext(provider.Object, out _, out var innerContext);

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CallActivityAsync(context, "RemoteActivity", null, withRetry, isOrchestratorThread: false));

            Assert.Contains("Multithreaded execution was detected", error.Message);
            innerContext.VerifyNoOtherCalls();
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityWithRetry_ProviderPolicyDoesNotAllowNullRetryOptions()
        {
            var provider = CreateProvider(allowsExternalActivityScheduling: true);
            DurableOrchestrationContext context = CreateContext(provider.Object, out _, out var innerContext);

            ArgumentNullException error = await Assert.ThrowsAsync<ArgumentNullException>(
                () => InvokeWithOrchestratorThread(
                    () => ((IDurableOrchestrationContext)context).CallActivityWithRetryAsync<string>(
                        "RemoteActivity", null!, null)));

            Assert.Equal("retryOptions", error.ParamName);
            innerContext.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityWithRetry_ProviderPolicyDoesNotBypassDelayValidation(bool rejectMaxInterval)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling: true);
            DurableOrchestrationContext context = CreateContext(provider.Object, out _, out var innerContext);
            TimeSpan rejectedDelay = TimeSpan.FromSeconds(5);
            string validationError = "The test provider does not support this retry interval.";
            provider.Setup(p => p.ValidateDelayTime(rejectedDelay, out validationError)).Returns(false);
            var retryOptions = new RetryOptions(TimeSpan.FromSeconds(1), 3)
            {
                MaxRetryInterval = TimeSpan.FromSeconds(10),
            };
            if (rejectMaxInterval)
            {
                retryOptions.MaxRetryInterval = rejectedDelay;
            }
            else
            {
                retryOptions.FirstRetryInterval = rejectedDelay;
            }

            ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(
                () => InvokeWithOrchestratorThread(
                    () => ((IDurableOrchestrationContext)context).CallActivityWithRetryAsync<string>(
                        "RemoteActivity", retryOptions, null)));

            Assert.Contains(validationError, error.Message);
            Assert.Equal(rejectMaxInterval ? "MaxRetryInterval" : "FirstRetryInterval", error.ParamName);
            innerContext.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ProviderPolicy_DoesNotAllowUnregisteredOrchestratorsOrEntities(bool callEntity)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling: true);
            DurableOrchestrationContext context = CreateContext(provider.Object, out _, out var innerContext);
            var client = (IDurableOrchestrationContext)context;

            ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(
                () => InvokeWithOrchestratorThread(
                    () => callEntity
                        ? client.CallEntityAsync<string>(new EntityId("RemoteEntity", "key"), "Get", null)
                        : client.CallSubOrchestratorAsync<string>("RemoteOrchestrator", "child-id", null)));

            Assert.Contains(callEntity ? "not an entity function" : "not an orchestrator function", error.Message);
            innerContext.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ProviderPolicy_PreservesSchedulingValidationForIndexedInactiveOrchestratorsAndEntities(
            bool allowsExternalActivityScheduling)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling);
            CreateContext(provider.Object, out var extension, out _);
            extension.RegisterOrchestrator(new FunctionName("LocalOrchestrator"), orchestratorInfo: null);
            extension.RegisterEntity(new FunctionName("LocalEntity"), entityInfo: null);

            extension.ThrowIfFunctionDoesNotExist("LocalOrchestrator", FunctionType.Orchestrator);
            extension.ThrowIfFunctionDoesNotExist("LocalEntity", FunctionType.Entity);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ProviderPolicy_PreservesBuiltInHttpActivityRegistrationBypass(bool allowsExternalActivityScheduling)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling);
            CreateContext(provider.Object, out var extension, out _);

            extension.ThrowIfFunctionDoesNotExist(HttpOptions.HttpTaskActivityReservedName, FunctionType.Activity);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ProviderPolicy_DoesNotMakeUnavailableActivitiesExecutable(bool disabled)
        {
            var provider = CreateProvider(allowsExternalActivityScheduling: true);
            CreateContext(provider.Object, out var extension, out _);
            if (disabled)
            {
                extension.RegisterActivity(new FunctionName("RemoteActivity"), executor: null!);
            }

            extension.ThrowIfFunctionDoesNotExist("RemoteActivity", FunctionType.Activity);
            var manager = (INameVersionObjectManager<TaskActivity>)extension;
            var shim = Assert.IsType<TaskNonexistentActivityShim>(
                manager.GetObject("RemoteActivity", string.Empty));
            var taskContext = new TaskContext(
                new OrchestrationInstance { InstanceId = "test-instance", ExecutionId = "test-execution" });

            TaskFailureException error = Assert.Throws<TaskFailureException>(() => shim.Run(taskContext, "null"));

            Assert.Contains(disabled ? "is disabled" : "does not exist", error.Message);
        }

        private static Mock<DurabilityProvider> CreateProvider(bool? allowsExternalActivityScheduling = null)
        {
            var provider = new Mock<DurabilityProvider>(
                "TestProvider",
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "TestConnection")
            {
                CallBase = true,
            };
            if (allowsExternalActivityScheduling.HasValue)
            {
                provider.SetupGet(p => p.AllowsExternalActivityScheduling).Returns(allowsExternalActivityScheduling.Value);
            }

            return provider;
        }

        private static DurableOrchestrationContext CreateContext(
            DurabilityProvider provider,
            out DurableTaskExtension extension,
            out Mock<OrchestrationContext> innerContext,
            int maxActions = 100000)
        {
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                MaxOrchestrationActions = maxActions,
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
            };
            options.StorageProvider["type"] = "TestProvider";
            var factory = new Mock<IDurabilityProviderFactory>();
            factory.SetupGet(f => f.Name).Returns("TestProvider");
            factory.Setup(f => f.GetDurabilityProvider()).Returns(provider);
            extension = new DurableTaskExtension(
                new OptionsWrapper<DurableTaskOptions>(options),
                NullLoggerFactory.Instance,
                TestHelpers.GetTestNameResolver(),
                [factory.Object],
                new TestHostShutdownNotificationService(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService());
            innerContext = new Mock<OrchestrationContext> { CallBase = true };
            return new DurableOrchestrationContext(extension, provider, "TestOrchestrator")
            {
                InstanceId = "parent-id",
                InnerContext = innerContext.Object,
            };
        }

        private static Task<string> CallActivityAsync(
            DurableOrchestrationContext context,
            string? name,
            object? input,
            bool withRetry,
            bool isOrchestratorThread = true)
        {
            var client = (IDurableOrchestrationContext)context;
            return InvokeWithOrchestratorThread(
                () => withRetry
                    ? client.CallActivityWithRetryAsync<string>(name!, new RetryOptions(TimeSpan.FromSeconds(1), 3), input)
                    : client.CallActivityAsync<string>(name!, input),
                isOrchestratorThread);
        }

        private static Task<string> InvokeWithOrchestratorThread(
            Func<Task<string>> action, bool isOrchestratorThread = true)
        {
            bool originalIsOrchestratorThread = OrchestrationContext.IsOrchestratorThread;
            OrchestrationContext.IsOrchestratorThread = isOrchestratorThread;
            try
            {
                return action();
            }
            finally
            {
                OrchestrationContext.IsOrchestratorThread = originalIsOrchestratorThread;
            }
        }
    }
}
