// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.Command;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
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

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ThrowIfOrchestratorFunctionIsDisabled_ActiveOutOfProcOrchestrator_DoesNotThrow()
        {
            var extension = CreateExtension();
            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            extension.RegisterOrchestrator(
                new FunctionName("ActiveOrchestrator"),
                new RegisteredFunctionInfo(mockExecutor.Object, isOutOfProc: true));

            extension.ThrowIfOrchestratorFunctionIsDisabled("ActiveOrchestrator");
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ThrowIfOrchestratorFunctionIsDisabled_MissingOrchestrator_DoesNotThrow()
        {
            var extension = CreateExtension();

            extension.ThrowIfOrchestratorFunctionIsDisabled("MissingOrchestrator");
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ThrowIfFunctionDoesNotExist_DisabledOrchestrator_DoesNotBreakReplay()
        {
            // Deterministic orchestration calls use this helper while replaying history. A disabled
            // orchestrator remains a known function, so availability checks belong only at new-start
            // entry points and must not make existing sub-orchestration history fail to replay.
            var extension = CreateExtension();
            extension.RegisterOrchestrator(new FunctionName("DisabledOrchestrator"), orchestratorInfo: null);

            extension.ThrowIfFunctionDoesNotExist("DisabledOrchestrator", FunctionType.Orchestrator);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ThrowIfFunctionDoesNotExist_UnknownActivity_Throws_ByDefault()
        {
            var extension = CreateExtension();

            Assert.Throws<ArgumentException>(() => extension.ThrowIfFunctionDoesNotExist("ExternalActivity", FunctionType.Activity));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ThrowIfFunctionDoesNotExist_UnknownActivity_Allowed_WhenAllowExternalActivities()
        {
            var extension = CreateExtension(allowExternalActivities: true);

            extension.ThrowIfFunctionDoesNotExist("ExternalActivity", FunctionType.Activity);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void ThrowIfFunctionDoesNotExist_UnknownOrchestrator_StillThrows_WhenAllowExternalActivities()
        {
            var extension = CreateExtension(allowExternalActivities: true);

            Assert.Throws<ArgumentException>(() => extension.ThrowIfFunctionDoesNotExist("ExternalOrchestrator", FunctionType.Orchestrator));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_NullName_Throws_AndSchedulesNothing_WhenAllowExternalActivities()
        {
            (Task<string> call, ScheduleTaskOrchestratorAction[] scheduledTasks) = CallActivityWithExternalActivitiesAllowed(null);

            Assert.True(call.IsFaulted);
            await Assert.ThrowsAsync<ArgumentNullException>(() => call);
            Assert.Empty(scheduledTasks);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_EmptyName_Throws_AndSchedulesNothing_WhenAllowExternalActivities()
        {
            (Task<string> call, ScheduleTaskOrchestratorAction[] scheduledTasks) = CallActivityWithExternalActivitiesAllowed(string.Empty);

            Assert.True(call.IsFaulted);
            await Assert.ThrowsAsync<ArgumentException>(() => call);
            Assert.Empty(scheduledTasks);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void CallActivityAsync_UnregisteredActivity_Schedules_WhenAllowExternalActivities()
        {
            (Task<string> call, ScheduleTaskOrchestratorAction[] scheduledTasks) = CallActivityWithExternalActivitiesAllowed("ExternalActivity");

            Assert.False(call.IsCompleted);
            ScheduleTaskOrchestratorAction scheduledTask = Assert.Single(scheduledTasks);
            Assert.Equal("ExternalActivity", scheduledTask.Name);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void DisabledEntity_IsTreatedAsUnavailableByClassicDispatch()
        {
            // The classic (in-proc / HTTP-protocol) entity dispatch path in EntityMiddleware treats an
            // entity as unavailable when GetEntityInfo(name)?.Executor is null, which is exactly the
            // state of a disabled-but-still-deployed entity: the binding provider registers it during
            // indexing (null executor) and the disabled function's listener never replaces it. That
            // signal drives the deterministic per-operation failure path in TaskEntityShim.ExecuteBatch
            // instead of a null-executor dereference / poison loop. The full dispatch is covered
            // end-to-end by the Node/Python entity E2E tests.
            // See https://github.com/Azure/azure-functions-durable-extension/issues/3471.
            var extension = CreateExtension();

            // Disabled via a null RegisteredFunctionInfo — how the binding provider registers a
            // disabled entity during indexing.
            extension.RegisterEntity(new FunctionName("DisabledEntityNullInfo"), entityInfo: null);

            // Defense-in-depth: a non-null info whose executor is null must also be treated as unavailable.
            extension.RegisterEntity(new FunctionName("DisabledEntityNullExecutor"), new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));

            // A normally registered entity is available.
            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            extension.RegisterEntity(new FunctionName("ActiveEntity"), new RegisteredFunctionInfo(mockExecutor.Object, isOutOfProc: true));

            // The unavailable entities have no active executor (EntityMiddleware's functionUnavailable check).
            Assert.Null(extension.GetEntityInfo(new FunctionName("DisabledEntityNullInfo"))?.Executor);

            RegisteredFunctionInfo nullExecutorInfo = extension.GetEntityInfo(new FunctionName("DisabledEntityNullExecutor"));
            Assert.NotNull(nullExecutorInfo);
            Assert.Null(nullExecutorInfo.Executor);
            Assert.False(nullExecutorInfo.HasActiveListener);

            // The active entity is runnable.
            RegisteredFunctionInfo activeInfo = extension.GetEntityInfo(new FunctionName("ActiveEntity"));
            Assert.NotNull(activeInfo);
            Assert.NotNull(activeInfo.Executor);
            Assert.True(activeInfo.HasActiveListener);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void GetInvalidEntityFunctionMessage_ListsRegisteredEntities_WhenNoOrchestratorsExist()
        {
            // Guards the diagnosability of the disabled-entity failure path: the message must reflect
            // the registered ENTITIES, not orchestrators. Previously the helper checked
            // knownOrchestrators.Count to decide whether to list entities, so with entities registered
            // but no orchestrators it wrongly reported "No entity functions are currently registered!".
            var extension = CreateExtension();
            extension.RegisterEntity(new FunctionName("MyEntity"), new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));

            string message = extension.GetInvalidEntityFunctionMessage("SomeMissingEntity");

            Assert.Contains("MyEntity", message);
            Assert.DoesNotContain("No entity functions are currently registered", message);
        }

        private static DurableTaskExtension CreateExtension(bool allowExternalActivities = false)
        {
            var options = new DurableTaskOptions
            {
                HubName = "TestHub",
                WebhookUriProviderOverride = () => new Uri("https://localhost"),
                AllowExternalActivities = allowExternalActivities,
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

        private static (Task<string> Call, ScheduleTaskOrchestratorAction[] ScheduledTasks) CallActivityWithExternalActivitiesAllowed(string? activityName)
        {
            var extension = CreateExtension(allowExternalActivities: true);
            var durabilityProvider = new DurabilityProvider(
                "test",
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object,
                "test");

            Task<string>? call = null;
            var orchestration = new DelegateOrchestration(innerContext =>
            {
                var context = new DurableOrchestrationContext(extension, durabilityProvider, "TestOrchestrator")
                {
                    InnerContext = innerContext,
                };
                call = ((IDurableOrchestrationContext)context).CallActivityAsync<string>(activityName!, "input");
            });

            var runtimeState = new OrchestrationRuntimeState(new List<HistoryEvent>
            {
                new OrchestratorStartedEvent(-1),
                new ExecutionStartedEvent(-1, null)
                {
                    Name = "TestOrchestrator",
                    Version = string.Empty,
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = "test-instance", ExecutionId = "test-execution" },
                },
            });

            OrchestratorExecutionResult result = new TaskOrchestrationExecutor(
                runtimeState,
                orchestration,
                BehaviorOnContinueAsNew.Carryover,
                ErrorPropagationMode.SerializeExceptions).Execute();

            return (call!, result.Actions.OfType<ScheduleTaskOrchestratorAction>().ToArray());
        }

        private sealed class DelegateOrchestration : TaskOrchestration
        {
            private readonly Action<OrchestrationContext> body;

            public DelegateOrchestration(Action<OrchestrationContext> body)
            {
                this.body = body;
            }

            public override Task<string> Execute(OrchestrationContext context, string input)
            {
                this.body(context);
                return Task.FromResult(string.Empty);
            }

            public override string GetStatus()
            {
                return string.Empty;
            }

            public override void RaiseEvent(OrchestrationContext context, string name, string input)
            {
            }
        }
    }
}
