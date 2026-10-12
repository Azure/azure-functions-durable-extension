// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Google.Protobuf;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OrchestrationCompletionOutcomeTests
    {
        private readonly ITestOutputHelper output;

        public OrchestrationCompletionOutcomeTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData(OrchestrationStatus.Completed, "Completed")]
        [InlineData(OrchestrationStatus.ContinuedAsNew, "ContinuedAsNew")]
        [InlineData(OrchestrationStatus.Failed, "Failed:safe rejection")]
        [InlineData(OrchestrationStatus.Terminated, "Terminated:safe rejection")]
        [InlineData(OrchestrationStatus.Running, null)]
        [InlineData(OrchestrationStatus.Suspended, null)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Completion_ReportsAcceptedOutcomeOnlyAfterAcknowledgement(OrchestrationStatus status, string? expected)
        {
            using var fixture = new Fixture(this.output);
            using var activity = new Activity("completion-test").Start();
            DurableTaskExtension.TagActivityWithOrchestrationStatus(OrchestrationRuntimeStatus.Running, "instance");
            OrchestrationRuntimeState state = await fixture.InvokePassthroughAsync(P.OrchestrationStatus.Completed);
            Assert.Empty(fixture.Notifications.Events);
            Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), IsTerminalLog);

            Task completion = fixture.CompleteAsync(state);
            Assert.Equal(1, fixture.Provider.CompletionCalls);
            Assert.Empty(fixture.Notifications.Events);
            Assert.Equal("Running", activity.GetTagItem("DurableFunctionsRuntimeStatus"));

            fixture.Provider.Acknowledgement.SetResult(CreateOutcome(status));
            await completion;

            Assert.Equal(status.ToString(), activity.GetTagItem("DurableFunctionsRuntimeStatus"));
            Assert.Single(activity.Tags, tag => tag.Key == "DurableFunctionsRuntimeStatus");
            Assert.Equal(expected == null ? Array.Empty<string>() : new[] { expected }, fixture.Notifications.Events);
            fixture.Service.VerifyNoOtherCalls();
            LogMessage acknowledgement = Assert.Single(fixture.Logs.GetAllLogMessages(), message =>
                message.FormattedMessage.Contains("Orchestration completion acknowledged"));
            Assert.Equal("completion", acknowledgement.State.Single(field => field.Key == "completionIdentity").Value);
            Assert.Equal(true, acknowledgement.State.Single(field => field.Key == "isAuthoritative").Value);
            if (status == OrchestrationStatus.Failed)
            {
                LogMessage failed = Assert.Single(fixture.Logs.GetAllLogMessages(), message => message.Level == LogLevel.Error);
                Assert.Contains("safe rejection", failed.FormattedMessage);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, failed.State.Single(field => field.Key == "runtimeStatus").Value);
                Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), message => message.FormattedMessage.Contains("State: Completed"));
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Completion_SubmissionFailureDoesNotReportTerminalOutcome()
        {
            using var fixture = new Fixture(this.output);
            OrchestrationRuntimeState state = await fixture.InvokePassthroughAsync(P.OrchestrationStatus.Completed);
            Task completion = fixture.CompleteAsync(state);
            fixture.Provider.Acknowledgement.SetException(new InvalidOperationException("submission failed"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => completion);
            Assert.Empty(fixture.Notifications.Events);
            Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), IsTerminalLog);
            fixture.Service.VerifyNoOtherCalls();
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Completion_NotificationFailureDoesNotRetryAcknowledgedWork()
        {
            using var fixture = new Fixture(this.output);
            fixture.Notifications.FailDelivery = true;
            fixture.Provider.Acknowledgement.SetResult(CreateOutcome(OrchestrationStatus.Failed));

            await fixture.CompleteAsync(CreateState());

            Assert.Equal(1, fixture.Provider.CompletionCalls);
            fixture.Service.VerifyNoOtherCalls();
            Assert.Contains(fixture.Logs.GetAllLogMessages(), message =>
                message.Level == LogLevel.Warning && message.FormattedMessage.Contains("will not be retried"));
            Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), message =>
                message.FormattedMessage.Contains("PRIVATE_NOTIFICATION_RESPONSE"));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Completion_ApplicationFailureHasOnePostAcknowledgementNotification()
        {
            using var fixture = new Fixture(this.output);
            OrchestrationRuntimeState state = await fixture.InvokePassthroughAsync(P.OrchestrationStatus.Failed);
            Assert.Empty(fixture.Notifications.Events);
            Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), IsTerminalLog);
            fixture.Provider.Acknowledgement.SetResult(new OrchestrationCompletionOutcome(
                "instance",
                "execution",
                "TestOrchestrator",
                "completion",
                OrchestrationStatus.Failed,
                isAuthoritative: true,
                failureDetails: new FailureDetails("ApplicationFailure", "application failed", null, null, false)));

            await fixture.CompleteAsync(state);

            Assert.Equal(new[] { "Failed:application failed" }, fixture.Notifications.Events);
            Assert.Single(fixture.Logs.GetAllLogMessages(), message => message.Level == LogLevel.Error);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Completion_LegacyBackendAcknowledgementUsesExplicitCompatibilityFallback()
        {
            using var fixture = new Fixture(this.output);
            fixture.Provider.Acknowledgement.SetResult(new OrchestrationCompletionOutcome(
                "instance", "execution", "TestOrchestrator", "completion", OrchestrationStatus.Completed, isAuthoritative: false));

            await fixture.CompleteAsync(CreateState());

            Assert.Equal(new[] { "Completed" }, fixture.Notifications.Events);
            Assert.Contains(fixture.Logs.GetAllLogMessages(), message =>
                message.Level == LogLevel.Warning && message.FormattedMessage.Contains("without returning its accepted outcome"));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Completion_EntityWithNoOrchestrationOutcomeDoesNotNotify()
        {
            using var fixture = new Fixture(this.output);
            fixture.Provider.Acknowledgement.SetResult(null);
            await fixture.CompleteAsync(CreateState());
            Assert.Empty(fixture.Notifications.Events);
            Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), IsTerminalLog);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task LegacyShim_DefersTerminalReportingOnlyForCapableProviders(bool supportsOutcomes)
        {
            using var fixture = new Fixture(this.output, supportsOutcomes);
            fixture.Extension.RegisterOrchestrator(
                new FunctionName("TestOrchestrator"),
                new RegisteredFunctionInfo(Mock.Of<ITriggeredFunctionExecutor>(), isOutOfProc: false));
            var shim = new TaskOrchestrationShim(fixture.Extension, fixture.ActiveProvider, "TestOrchestrator");
            var context = (DurableOrchestrationContext)shim.Context;
            context.InstanceId = "instance";
            context.ExecutionId = "execution";
            shim.SetFunctionInvocationCallback(() => Task.FromResult<object>("done"));
            bool wasOrchestratorThread = OrchestrationContext.IsOrchestratorThread;
            OrchestrationContext.IsOrchestratorThread = true;
            try
            {
                await shim.Execute(Mock.Of<OrchestrationContext>(), null!);
                await context.RunDeferredTasks();
            }
            finally
            {
                OrchestrationContext.IsOrchestratorThread = wasOrchestratorThread;
            }

            if (supportsOutcomes)
            {
                Assert.DoesNotContain("Completed", fixture.Notifications.Events);
                Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), IsTerminalLog);
                fixture.Provider.Acknowledgement.SetResult(CreateOutcome(OrchestrationStatus.Failed));
                await fixture.CompleteAsync(CreateState());
                Assert.Equal(new[] { "Started", "Failed:safe rejection" }, fixture.Notifications.Events);
            }
            else
            {
                Assert.Equal(new[] { "Started", "Completed" }, fixture.Notifications.Events);
                await fixture.CompleteAsync(CreateState());
                fixture.Service.Verify(
                    service => service.CompleteTaskOrchestrationWorkItemAsync(
                        It.IsAny<TaskOrchestrationWorkItem>(),
                        It.IsAny<OrchestrationRuntimeState>(),
                        It.IsAny<IList<TaskMessage>>(),
                        It.IsAny<IList<TaskMessage>>(),
                        It.IsAny<IList<TaskMessage>>(),
                        It.IsAny<TaskMessage>(),
                        It.IsAny<OrchestrationState>()),
                    Times.Once);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Passthrough_ExistingProviderStillReportsCompletionBeforeSubmission(bool supportsOutcomes)
        {
            using var fixture = new Fixture(this.output, supportsOutcomes);
            await fixture.InvokePassthroughAsync(P.OrchestrationStatus.Completed);
            Assert.Equal(supportsOutcomes ? Array.Empty<string>() : new[] { "Completed" }, fixture.Notifications.Events);
            Assert.Equal(!supportsOutcomes, fixture.Logs.GetAllLogMessages().Any(IsTerminalLog));
            Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), message => message.FormattedMessage.Contains("State: Awaited"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task LegacyShim_DefersApplicationFailureOnlyForCapableProviders(bool supportsOutcomes)
        {
            using var fixture = new Fixture(this.output, supportsOutcomes);
            fixture.Extension.RegisterOrchestrator(
                new FunctionName("TestOrchestrator"),
                new RegisteredFunctionInfo(Mock.Of<ITriggeredFunctionExecutor>(), isOutOfProc: false));
            var shim = new TaskOrchestrationShim(fixture.Extension, fixture.ActiveProvider, "TestOrchestrator");
            var context = (DurableOrchestrationContext)shim.Context;
            context.InstanceId = "instance";
            context.ExecutionId = "execution";
            shim.SetFunctionInvocationCallback(() => Task.FromException<object>(new InvalidOperationException("application failed")));
            OrchestrationContext innerContext = Mock.Of<OrchestrationContext>();
            innerContext.ErrorDataConverter = fixture.Extension.ErrorDataConverter;
            await Assert.ThrowsAsync<OrchestrationFailureException>(
                () => shim.Execute(innerContext, null!));
            await context.RunDeferredTasks();

            Assert.Equal(supportsOutcomes ? new[] { "Started" } : new[] { "Started", "Failed:application failed" }, fixture.Notifications.Events);
            Assert.Equal(!supportsOutcomes, fixture.Logs.GetAllLogMessages().Any(IsTerminalLog));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Completion_UsesCapturedHandlerDuringHostDisposal()
        {
            using var fixture = new Fixture(this.output);
            Task completion = fixture.CompleteAsync(CreateState());
            fixture.Extension.Dispose();
            fixture.Provider.Acknowledgement.SetResult(CreateOutcome(OrchestrationStatus.Failed));
            await completion;
            Assert.Equal(new[] { "Failed:safe rejection" }, fixture.Notifications.Events);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Passthrough_TerminationIsReportedOnlyAfterAcknowledgement()
        {
            using var fixture = new Fixture(this.output);
            OrchestrationRuntimeState state = await fixture.InvokePassthroughAsync(
                P.OrchestrationStatus.Completed, hasTerminationEvent: true);
            Assert.Empty(fixture.Notifications.Events);
            Assert.DoesNotContain(fixture.Logs.GetAllLogMessages(), IsTerminalLog);
            fixture.Provider.Acknowledgement.SetResult(new OrchestrationCompletionOutcome(
                "instance", "execution", null, "completion", OrchestrationStatus.Terminated, isAuthoritative: true));

            await fixture.CompleteAsync(state);

            Assert.Equal(new[] { "Terminated:termination reason" }, fixture.Notifications.Events);
            Assert.Contains(fixture.Logs.GetAllLogMessages(), message =>
                message.State.Any(field => field.Key == "runtimeStatus" && field.Value.Equals(OrchestrationRuntimeStatus.Terminated)));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void Outcome_RejectsInvalidProviderFailureAndNegativeSizes()
        {
            Assert.Throws<ArgumentException>(() => new OrchestrationCompletionOutcome(
                "instance", null, null, "completion", OrchestrationStatus.Completed, true, isProviderGeneratedFailure: true));
            Assert.Throws<ArgumentException>(() => new OrchestrationCompletionOutcome(
                "instance", null, null, "completion", OrchestrationStatus.Failed, true, isProviderGeneratedFailure: true));
            Assert.Throws<ArgumentException>(() => new OrchestrationCompletionOutcome(
                "instance", null, null, "", OrchestrationStatus.Running, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OrchestrationCompletionOutcome(
                "instance", null, null, "completion", OrchestrationStatus.Running, true, requestSizeBytes: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OrchestrationCompletionOutcome(
                "instance", null, null, "completion", OrchestrationStatus.Running, true, sizeLimitBytes: -1));
        }

        private static bool IsTerminalLog(LogMessage message) =>
            message.FormattedMessage.Contains("State: Completed") || message.FormattedMessage.Contains("State: Failed");

        private static OrchestrationCompletionOutcome CreateOutcome(OrchestrationStatus status) =>
            new OrchestrationCompletionOutcome(
                "instance",
                "execution",
                "TestOrchestrator",
                "completion",
                status,
                isAuthoritative: true,
                isProviderGeneratedFailure: status == OrchestrationStatus.Failed,
                failureDetails: status == OrchestrationStatus.Failed ? new FailureDetails("InvalidOperationException", "safe rejection", null, null, true) : null,
                reportingReason: "safe rejection",
                requestSizeBytes: 5_000_000,
                sizeLimitBytes: 4_089_446,
                sizeLimitType: "CompletionRequest");

        private static OrchestrationRuntimeState CreateState() =>
            new OrchestrationRuntimeState(new[]
            {
                new ExecutionStartedEvent(0, null)
                {
                    Name = "TestOrchestrator",
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "instance", ExecutionId = "execution" },
                },
            });

        private sealed class Fixture : IDisposable
        {
            private readonly ILoggerFactory loggerFactory;

            public Fixture(ITestOutputHelper output, bool supportsOutcomes = true)
            {
                this.Logs = new TestLoggerProvider(output);
                this.loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(this.Logs));
                this.Service = new Mock<IOrchestrationService>();
                this.Service.Setup(service => service.CompleteTaskOrchestrationWorkItemAsync(
                    It.IsAny<TaskOrchestrationWorkItem>(),
                    It.IsAny<OrchestrationRuntimeState>(),
                    It.IsAny<IList<TaskMessage>>(),
                    It.IsAny<IList<TaskMessage>>(),
                    It.IsAny<IList<TaskMessage>>(),
                    It.IsAny<TaskMessage>(),
                    It.IsAny<OrchestrationState>())).Returns(Task.CompletedTask);
                this.Provider = new OutcomeProvider(this.Service.Object);
                this.ActiveProvider = supportsOutcomes
                    ? this.Provider
                    : new DurabilityProvider("test", this.Service.Object, Mock.Of<IOrchestrationServiceClient>(), "test");
                var factory = new Mock<IDurabilityProviderFactory>();
                factory.SetupGet(value => value.Name).Returns(AzureStorageDurabilityProviderFactory.ProviderName);
                factory.Setup(value => value.GetDurabilityProvider()).Returns(this.ActiveProvider);
                this.Notifications = new RecordingNotifications();
                this.Extension = new DurableTaskExtension(
                    new OptionsWrapper<DurableTaskOptions>(new DurableTaskOptions { HubName = "TestHub", WebhookUriProviderOverride = () => new Uri("http://localhost") }),
                    this.loggerFactory,
                    TestHelpers.GetTestNameResolver(),
                    new[] { factory.Object },
                    new TestHostShutdownNotificationService(),
                    lifeCycleNotificationHelper: this.Notifications,
                    platformInformationService: TestHelpers.GetMockPlatformInformationService());
            }

            public DurableTaskExtension Extension { get; }

            public Mock<IOrchestrationService> Service { get; }

            public OutcomeProvider Provider { get; }

            public DurabilityProvider ActiveProvider { get; }

            public RecordingNotifications Notifications { get; }

            public TestLoggerProvider Logs { get; }

            public Task CompleteAsync(OrchestrationRuntimeState state) =>
                this.ActiveProvider.CompleteTaskOrchestrationWorkItemAsync(
                    new TaskOrchestrationWorkItem { InstanceId = "instance", OrchestrationRuntimeState = state },
                    state,
                    new List<TaskMessage>(),
                    new List<TaskMessage>(),
                    new List<TaskMessage>(),
                    null!,
                    null!);

            public async Task<OrchestrationRuntimeState> InvokePassthroughAsync(P.OrchestrationStatus status, bool hasTerminationEvent = false)
            {
                var response = new P.OrchestratorResponse();
                response.Actions.Add(new P.OrchestratorAction
                {
                    CompleteOrchestration = new P.CompleteOrchestrationAction
                    {
                        OrchestrationStatus = status,
                        Result = "\"done\"",
                        FailureDetails = status == P.OrchestrationStatus.Failed
                            ? new P.TaskFailureDetails { ErrorType = "ApplicationFailure", ErrorMessage = "application failed" }
                            : null,
                    },
                });
                string encoded = Convert.ToBase64String(response.ToByteArray());
                var executor = new Mock<ITriggeredFunctionExecutor>();
                executor.Setup(value => value.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                    .Returns(async (TriggeredFunctionData input, CancellationToken _) =>
                    {
                        try
                        {
#pragma warning disable CS0618
                            await input.InvokeHandler(() => Task.FromResult<object>(encoded));
#pragma warning restore CS0618
                            return new FunctionResult(true);
                        }
                        catch (Exception exception)
                        {
                            return new FunctionResult(false, exception);
                        }
                    });
                this.Extension.RegisterOrchestrator(new FunctionName("TestOrchestrator"), new RegisteredFunctionInfo(executor.Object, isOutOfProc: true));
                var dispatchContext = new DispatchMiddlewareContext();
                OrchestrationRuntimeState state = CreateState();
                if (hasTerminationEvent)
                {
                    state.AddEvent(new ExecutionTerminatedEvent(1, "termination reason"));
                }

                dispatchContext.SetProperty(state);
                dispatchContext.SetProperty(state.OrchestrationInstance);
                ConstructorInfo constructor = typeof(WorkItemMetadata).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool), typeof(bool) }, null)
                    ?? throw new InvalidOperationException("Work-item metadata constructor was not found.");
                dispatchContext.SetProperty((WorkItemMetadata)constructor.Invoke(new object[] { false, false }));
                await new OutOfProcMiddleware(this.Extension).CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask);
                return state;
            }

            public void Dispose()
            {
                this.Extension.Dispose();
                this.loggerFactory.Dispose();
            }
        }

        private sealed class OutcomeProvider : DurabilityProvider, IOrchestrationCompletionOutcomeProvider
        {
            public OutcomeProvider(IOrchestrationService service)
                : base("test", service, Mock.Of<IOrchestrationServiceClient>(), "test")
            {
            }

            public TaskCompletionSource<OrchestrationCompletionOutcome?> Acknowledgement { get; } =
                new TaskCompletionSource<OrchestrationCompletionOutcome?>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int CompletionCalls { get; private set; }

            public Task<OrchestrationCompletionOutcome?> CompleteTaskOrchestrationWorkItemWithOutcomeAsync(
                TaskOrchestrationWorkItem workItem,
                OrchestrationRuntimeState newOrchestrationRuntimeState,
                IList<TaskMessage> outboundMessages,
                IList<TaskMessage> orchestratorMessages,
                IList<TaskMessage> timerMessages,
                TaskMessage? continuedAsNewMessage,
                OrchestrationState? orchestrationState)
            {
                this.CompletionCalls++;
                return this.Acknowledgement.Task;
            }
        }

        private sealed class RecordingNotifications : ILifeCycleNotificationHelper
        {
            public List<string> Events { get; } = new List<string>();

            public bool FailDelivery { get; set; }

            public Task OrchestratorStartingAsync(string hubName, string functionName, string instanceId, bool isReplay)
            {
                this.Events.Add("Started");
                return Task.CompletedTask;
            }

            public Task OrchestratorCompletedAsync(string hubName, string functionName, string instanceId, bool continuedAsNew, bool isReplay)
            {
                this.Events.Add(continuedAsNew ? "ContinuedAsNew" : "Completed");
                return Task.CompletedTask;
            }

            public Task OrchestratorFailedAsync(string hubName, string functionName, string instanceId, string reason, bool isReplay)
            {
                this.Events.Add($"Failed:{reason}");
                return this.FailDelivery ? Task.FromException(new InvalidOperationException("PRIVATE_NOTIFICATION_RESPONSE")) : Task.CompletedTask;
            }

            public Task OrchestratorTerminatedAsync(string hubName, string functionName, string instanceId, string reason)
            {
                this.Events.Add($"Terminated:{reason}");
                return Task.CompletedTask;
            }
        }
    }
}
