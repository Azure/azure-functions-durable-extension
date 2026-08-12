// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Google.Protobuf;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OutOfProcMiddlewareTests
    {
        private const string NoWorkerInitializedMessage = "Did not find any initialized language workers";
        private const string AssemblyNotLoadedMessage = "Could not load file or assembly";

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallOrchestratorAsync_DifferentInvalidOperationException_DoesNotThrowSessionAbortedException()
        {
            // Arrange: a different InvalidOperationException message should NOT trigger the retry path
            var innerException = new InvalidOperationException("The internal function invoker returned a task that does not support return values!");
            var outerException = new Exception("Function invocation failed.", innerException);

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupOrchestratorTest(outerException);

            // Act: should NOT throw SessionAbortedException — instead the orchestration should be marked as failed
            await middleware.CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: the middleware should have set a failure result on the dispatch context
            OrchestratorExecutionResult result = dispatchContext.GetProperty<OrchestratorExecutionResult>();
            Assert.NotNull(result);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(PlatformLevelExceptions))]
        public async Task CallOrchestratorAsync_PlatformLevelException_ThrowsSessionAbortedException(Exception exception)
        {
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupOrchestratorTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_FunctionTimeoutAbortException_ThrowsSessionAbortedException()
        {
            var exception = new FunctionTimeoutAbortException("Activity A timed out! Worker channel closing");

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupEntityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallEntityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_WorkerNotInitializedException_ThrowsSessionAbortedException()
        {
            // Arrange: an InvalidOperationException with the "Did not find any initialized language workers" message
            // indicates the worker is not yet ready and the entity should be retried.
            var exception = new Exception(
                "Function invocation failed.",
                new InvalidOperationException(NoWorkerInitializedMessage));

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupEntityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallEntityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_AssemblyNotLoadedException_ThrowsSessionAbortedException()
        {
            // Arrange: a FileNotFoundException with the "Could not load file or assembly" message
            // indicates the worker assembly is not yet loaded and the entity should be retried.
            var exception = new Exception(
                "Function invocation failed.",
                new FileNotFoundException(AssemblyNotLoadedMessage));

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupEntityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallEntityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_FunctionTimeoutAbortException_ThrowsSessionAbortedException()
        {
            var exception = new FunctionTimeoutAbortException("Activity A timed out! Worker channel closing");

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupActivityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_WorkerNotInitializedException_ThrowsSessionAbortedException()
        {
            // Arrange: an InvalidOperationException with the "Did not find any initialized language workers" message
            // indicates the worker is not yet ready and the activity should be retried.
            var exception = new Exception(
                "Function invocation failed.",
                new InvalidOperationException(NoWorkerInitializedMessage));

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupActivityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_AssemblyNotLoadedException_ThrowsSessionAbortedException()
        {
            // Arrange: a FileNotFoundException with the "Could not load file or assembly" message
            // indicates the worker assembly is not yet loaded and the activity should be retried.
            var exception = new Exception(
                "Function invocation failed.",
                new FileNotFoundException(AssemblyNotLoadedMessage));

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupActivityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_DisabledActivity_FailsDeterministicallyWithoutAbort()
        {
            // Reproduces https://github.com/Azure/azure-functions-durable-extension/issues/3471 for the
            // passthrough/out-of-proc activity middleware. A disabled-but-still-deployed activity is
            // indexed (present in knownActivities) but has a null executor because its listener never
            // started. The middleware must fail the activity with a deterministic TaskFailedEvent instead
            // of dereferencing the null executor (which surfaced as a SessionAbortedException and made the
            // work item retry forever).
            DurableTaskExtension extension = CreateDurableTaskExtension();
            extension.RegisterActivity(new FunctionName("TestActivity"), executor: null!);

            var middleware = new OutOfProcMiddleware(extension);
            var dispatchContext = new DispatchMiddlewareContext();
            dispatchContext.SetProperty(new TaskScheduledEvent(-1) { Name = "TestActivity" });
            dispatchContext.SetProperty(new OrchestrationInstance { InstanceId = "test-instance-id" });

            // Act: must NOT throw (no SessionAbortedException / NullReferenceException poison loop).
            await middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: a deterministic (non-transient) failure was set on the dispatch context.
            ActivityExecutionResult result = dispatchContext.GetProperty<ActivityExecutionResult>();
            Assert.NotNull(result);
            TaskFailedEvent failedEvent = Assert.IsType<TaskFailedEvent>(result.ResponseEvent);
            Assert.Contains("TestActivity", failedEvent.Reason);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_DisabledEntity_FailsDeterministicallyWithoutAbort()
        {
            // Reproduces https://github.com/Azure/azure-functions-durable-extension/issues/3471 for the
            // passthrough/out-of-proc entity middleware. A disabled-but-still-deployed entity is indexed
            // (present in knownEntities) but has a null executor because its listener never started. The
            // middleware must fail the batch with a non-retriable FailureDetails result instead of
            // dereferencing the null executor (which surfaced as a transient failure and retried forever).
            DurableTaskExtension extension = CreateDurableTaskExtension();
            extension.RegisterEntity(new FunctionName("TestEntity"), new RegisteredFunctionInfo(executor: null!, isOutOfProc: true));

            var middleware = new OutOfProcMiddleware(extension);
            var dispatchContext = new DispatchMiddlewareContext();
            dispatchContext.SetProperty(new EntityBatchRequest
            {
                InstanceId = "@TestEntity@test-key",
                EntityState = null,
                Operations = new List<OperationRequest>(),
            });
            dispatchContext.SetProperty(CreateWorkItemMetadata(isExtendedSession: false, includeState: false));

            // Act: must NOT throw (no SessionAbortedException / NullReferenceException poison loop).
            await middleware.CallEntityAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: a deterministic, non-retriable failure was set on the dispatch context.
            EntityBatchResult result = dispatchContext.GetProperty<EntityBatchResult>();
            Assert.NotNull(result);
            Assert.NotNull(result.FailureDetails);
            Assert.Contains("TestEntity", result.FailureDetails.ErrorMessage);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TryGetStructuredFailureDetails_StructuredPayload_ReturnsFailureDetailsWithProperties()
        {
            // Arrange: an out-of-proc worker exception whose message embeds a serialized
            // TaskFailureDetails JSON payload (including custom Properties), in the
            // "Result: ...\nException: {json}\nStack: ..." format produced by the host.
            const string serializedFailureDetails =
                """{"errorType":"BusinessValidationException","errorMessage":"Business logic validation failed","stackTrace":"at BusinessActivity.Run()","isNonRetriable":false,"properties":{"StringProperty":"validation-error-123","IntProperty":100,"NullProperty":null}}""";
            var exception = new Exception(
                $"Result: failure\nException: {serializedFailureDetails}\nStack: at Worker.Invoke()");

            // Act
            FailureDetails details = OutOfProcMiddleware.TryGetStructuredFailureDetails(exception);

            // Assert: the structured error type, message, and custom properties are parsed.
            Assert.NotNull(details);
            Assert.Equal("BusinessValidationException", details.ErrorType);
            Assert.Equal("Business logic validation failed", details.ErrorMessage);
            Assert.NotNull(details.Properties);
            Assert.Equal("validation-error-123", details.Properties["StringProperty"]);

            // JSON numbers are parsed as protobuf number values, which surface as doubles.
            Assert.Equal(100d, Assert.IsType<double>(details.Properties["IntProperty"]));

            // A JSON null property is preserved as a null value (not dropped).
            Assert.True(details.Properties.ContainsKey("NullProperty"));
            Assert.Null(details.Properties["NullProperty"]);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TryGetStructuredFailureDetails_PayloadInInnerException_ReturnsFailureDetails()
        {
            // Arrange: the serialized payload is carried by the inner exception, which is the
            // common shape when the host wraps the worker failure in an outer exception.
            const string serializedFailureDetails =
                """{"errorType":"BusinessValidationException","errorMessage":"Business logic validation failed"}""";
            var exception = new Exception(
                "Function invocation failed.",
                new Exception($"Result: failure\nException: {serializedFailureDetails}\nStack: at Worker.Invoke()"));

            // Act
            FailureDetails details = OutOfProcMiddleware.TryGetStructuredFailureDetails(exception);

            // Assert
            Assert.NotNull(details);
            Assert.Equal("BusinessValidationException", details.ErrorType);
            Assert.Equal("Business logic validation failed", details.ErrorMessage);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TryGetStructuredFailureDetails_NonResultMessage_ReturnsNull()
        {
            // Arrange: an exception that does not carry an RPC "Result:" payload.
            var exception = new InvalidOperationException("Some arbitrary failure that is not an RPC result.");

            // Act + Assert: callers should fall back to legacy behavior.
            Assert.Null(OutOfProcMiddleware.TryGetStructuredFailureDetails(exception));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TryGetStructuredFailureDetails_NonJsonExceptionPayload_ReturnsNull()
        {
            // Arrange: an RPC "Result:" message whose exception payload is a plain string
            // rather than a serialized TaskFailureDetails JSON object.
            var exception = new Exception(
                "Result: failure\nException: System.ApplicationException: Kah-BOOOOM!!\nStack: at Worker.Invoke()");

            // Act + Assert: no structured payload, so null is returned.
            Assert.Null(OutOfProcMiddleware.TryGetStructuredFailureDetails(exception));
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("sayōnara")]
        [InlineData(null)]
        public async Task CallOrchestratorAsync_TerminatedInstance_RaisesTerminatedNotification(string terminationReason)
        {
            // Regression test for https://github.com/Azure/azure-functions-durable-extension/issues/286.
            // A termination is applied by the orchestration executor and never reaches orchestrator user
            // code, so the middleware is responsible for raising the "Terminated" lifecycle notification.
            // Both cases here are terminated; the null case covers terminating without supplying a reason.
            var notificationHelper = new RecordingLifeCycleNotificationHelper();
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupCompletedOrchestratorTest(notificationHelper, isTerminated: true, terminationReason);

            await middleware.CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask);

            Assert.Equal(new[] { $"Terminated:{terminationReason}" }, notificationHelper.Notifications);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallOrchestratorAsync_CompletedInstance_RaisesCompletedNotification()
        {
            // Control case for the regression above: an instance that completed normally must still
            // raise "Completed", so the termination handling does not swallow the ordinary path.
            var notificationHelper = new RecordingLifeCycleNotificationHelper();
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupCompletedOrchestratorTest(notificationHelper, isTerminated: false, terminationReason: null);

            await middleware.CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask);

            Assert.Equal(new[] { "Completed" }, notificationHelper.Notifications);
        }

        public static IEnumerable<object[]> PlatformLevelExceptions()
        {
            // FunctionTimeoutException (top-level)
            yield return new object[] { new Host.FunctionTimeoutException("Function timed out.") };

            // FunctionTimeoutAbortException (top-level)
            yield return new object[] { new Host.FunctionTimeoutAbortException("Function timed out.") };

            // SessionAbortedException as InnerException (e.g. out-of-memory handling)
            yield return new object[] { new Exception("Function invocation failed.", new SessionAbortedException("Out of memory")) };

            // WorkerProcessExitException as InnerException (matched by type name)
            yield return new object[] { new Exception("Function invocation failed.", new WorkerProcessExitExceptionStub("Worker process exited.")) };

            // InvalidOperationException with "No process is associated" as InnerException
            yield return new object[] { new Exception("Function invocation failed.", new InvalidOperationException("No process is associated with this object.")) };

            // GrpcChannelTemporarilyUnavailableException as InnerException (gRPC sidecar unavailable, wrapped by host)
            yield return new object[] { new Exception("Function invocation failed.", new GrpcChannelTemporarilyUnavailableException("The local gRPC endpoint is not available.")) };

            // GrpcChannelTemporarilyUnavailableException as top-level exception (thrown in-process during binding)
            yield return new object[] { new GrpcChannelTemporarilyUnavailableException("The local gRPC endpoint is not available.") };

            // InvalidOperationException with "Did not find any initialized language workers" as InnerException (worker not yet initialized)
            yield return new object[] { new Exception("Function invocation failed.", new InvalidOperationException(NoWorkerInitializedMessage)) };

            // FileNotFoundException with "Could not load file or assembly" as InnerException (assembly not yet loaded during initialization)
            yield return new object[] { new Exception("Function invocation failed.", new FileNotFoundException(AssemblyNotLoadedMessage)) };
        }

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) SetupOrchestratorTest(Exception executorException)
        {
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext)
                = this.CreateMiddleware(executorException, "TestOrchestrator", FunctionType.Orchestrator);

            var orchestrationState = new OrchestrationRuntimeState(
                [
                    new ExecutionStartedEvent(-1, null) { Name = "TestOrchestrator" },
                ]);

            dispatchContext.SetProperty(orchestrationState);
            dispatchContext.SetProperty(new OrchestrationInstance { InstanceId = "test-instance-id" });

            return (middleware, dispatchContext);
        }

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) SetupEntityTest(Exception executorException)
        {
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext)
                = this.CreateMiddleware(executorException, "TestEntity", FunctionType.Entity);

            dispatchContext.SetProperty(new EntityBatchRequest
            {
                InstanceId = "@TestEntity@test-key",
                EntityState = null,
                Operations = new List<OperationRequest>(),
            });

            return (middleware, dispatchContext);
        }

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) SetupActivityTest(Exception executorException)
        {
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext)
                = this.CreateMiddleware(executorException, "TestActivity", FunctionType.Activity);

            dispatchContext.SetProperty(new TaskScheduledEvent(-1) { Name = "TestActivity" });
            dispatchContext.SetProperty(new OrchestrationInstance { InstanceId = "test-instance-id" });

            return (middleware, dispatchContext);
        }

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) SetupCompletedOrchestratorTest(
            ILifeCycleNotificationHelper notificationHelper,
            bool isTerminated,
            string terminationReason)
        {
            DurableTaskExtension extension = CreateDurableTaskExtension(notificationHelper);

            // Model what a language worker returns after processing the work item: an orchestration
            // completion action whose status is either Terminated or Completed.
            var response = new P.OrchestratorResponse();
            response.Actions.Add(new P.OrchestratorAction
            {
                CompleteOrchestration = new P.CompleteOrchestrationAction
                {
                    OrchestrationStatus = isTerminated
                        ? P.OrchestrationStatus.Terminated
                        : P.OrchestrationStatus.Completed,
                    Result = terminationReason ?? "\"done\"",
                },
            });

            string encodedResponse = Convert.ToBase64String(response.ToByteArray());

            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            mockExecutor
                .Setup(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Returns(async (TriggeredFunctionData data, CancellationToken _) =>
                {
#pragma warning disable CS0618 // Approved for use by this extension
                    await data.InvokeHandler(() => Task.FromResult<object>(encodedResponse));
#pragma warning restore CS0618
                    return new FunctionResult(succeeded: true);
                });

            extension.RegisterOrchestrator(
                new FunctionName("TestOrchestrator"),
                new RegisteredFunctionInfo(mockExecutor.Object, isOutOfProc: true));

            // The ExecutionStartedEvent lands in PastEvents, so the instance is not treated as brand new
            // and no "Started" notification is expected. The termination event is a new event delivered
            // with this work item.
            var runtimeState = new OrchestrationRuntimeState(
                [
                    new ExecutionStartedEvent(-1, null) { Name = "TestOrchestrator" },
                ]);

            if (isTerminated)
            {
                runtimeState.AddEvent(new ExecutionTerminatedEvent(-1, terminationReason));
            }

            var dispatchContext = new DispatchMiddlewareContext();
            dispatchContext.SetProperty(CreateWorkItemMetadata(isExtendedSession: false, includeState: false));
            dispatchContext.SetProperty(runtimeState);
            dispatchContext.SetProperty(new OrchestrationInstance { InstanceId = "test-instance-id" });

            return (new OutOfProcMiddleware(extension), dispatchContext);
        }

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) CreateMiddleware(
            Exception executorException, string functionName, FunctionType functionType)
        {
            DurableTaskExtension extension = CreateDurableTaskExtension();

            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            mockExecutor
                .Setup(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FunctionResult(false, executorException));

            var name = new FunctionName(functionName);

            switch (functionType)
            {
                case FunctionType.Activity:
                    extension.RegisterActivity(name, mockExecutor.Object);
                    break;
                case FunctionType.Entity:
                    extension.RegisterEntity(name, new RegisteredFunctionInfo(mockExecutor.Object, isOutOfProc: true));
                    break;
                default:
                    extension.RegisterOrchestrator(name, new RegisteredFunctionInfo(mockExecutor.Object, isOutOfProc: true));
                    break;
            }

            var dispatchContext = new DispatchMiddlewareContext();

            // Orchestrators and entities require WorkItemMetadata; activities do not.
            if (functionType != FunctionType.Activity)
            {
                dispatchContext.SetProperty(CreateWorkItemMetadata(isExtendedSession: false, includeState: false));
            }

            return (new OutOfProcMiddleware(extension), dispatchContext);
        }

        private static DurableTaskExtension CreateDurableTaskExtension(
            ILifeCycleNotificationHelper lifeCycleNotificationHelper = null)
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
                lifeCycleNotificationHelper,
                platformInformationService: TestHelpers.GetMockPlatformInformationService());
        }

        private static WorkItemMetadata CreateWorkItemMetadata(bool isExtendedSession, bool includeState)
        {
            // WorkItemMetadata has an internal constructor, so we use reflection to create it.
            ConstructorInfo ctor = typeof(WorkItemMetadata).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(bool), typeof(bool)],
                modifiers: null);
            Assert.NotNull(ctor);
            return (WorkItemMetadata)ctor.Invoke([isExtendedSession, includeState]);
        }

        /// <summary>
        /// Records the lifecycle notifications raised by the middleware so tests can assert on them.
        /// </summary>
        private sealed class RecordingLifeCycleNotificationHelper : ILifeCycleNotificationHelper
        {
            public List<string> Notifications { get; } = new List<string>();

            public Task OrchestratorStartingAsync(string hubName, string functionName, string instanceId, bool isReplay)
            {
                this.Notifications.Add("Started");
                return Task.CompletedTask;
            }

            public Task OrchestratorCompletedAsync(string hubName, string functionName, string instanceId, bool continuedAsNew, bool isReplay)
            {
                this.Notifications.Add("Completed");
                return Task.CompletedTask;
            }

            public Task OrchestratorFailedAsync(string hubName, string functionName, string instanceId, string reason, bool isReplay)
            {
                this.Notifications.Add($"Failed:{reason}");
                return Task.CompletedTask;
            }

            public Task OrchestratorTerminatedAsync(string hubName, string functionName, string instanceId, string reason)
            {
                this.Notifications.Add($"Terminated:{reason}");
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Stub exception whose type name contains "WorkerProcessExitException" to match the
        /// string-based check in <see cref="OutOfProcMiddleware"/>. The real
        /// <c>WorkerProcessExitException</c> lives in <c>Microsoft.Azure.WebJobs.Script</c>
        /// (the Functions host runtime), which is too heavy to reference as a test dependency.
        /// </summary>
        private class WorkerProcessExitExceptionStub(string message)
            : Exception(message)
        {
        }
    }
}
