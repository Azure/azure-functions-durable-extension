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

#nullable enable
namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OutOfProcMiddlewareTests
    {
        private const string NoWorkerInitializedMessage = "Did not find any initialized language workers";
        private const string AssemblyNotLoadedMessage = "Could not load file or assembly";
        private const string WorkerDrainingMessageMarker = "[DurableTask:WorkerDraining]";

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
        public async Task CallActivityAsync_WorkerDrainingException_ThrowsSessionAbortedException()
        {
            // Arrange: an activity failure whose message contains the worker-draining marker indicates the
            // worker completed the activity while shutting down. The activity should be aborted and retried
            // on a healthy worker rather than recording a result produced during shutdown.
            var exception = new Exception(
                $"The worker is shutting down and will not commit this result. {WorkerDrainingMessageMarker}");

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupActivityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_WorkerDrainingException_NestedInInnerException_ThrowsSessionAbortedException()
        {
            // Arrange: the worker-draining marker may be nested in an inner exception after crossing the
            // gRPC boundary. The host should still detect it by walking the inner-exception chain.
            var exception = new Exception(
                "Function invocation failed.",
                new InvalidOperationException(
                    $"The worker is shutting down and will not commit this result. {WorkerDrainingMessageMarker}"));

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupActivityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallActivityAsync_UnrelatedFailure_DoesNotThrowSessionAbortedException()
        {
            // Arrange: an ordinary activity failure (no worker-draining marker, not a platform-level error)
            // should be reported as a failed activity result, NOT trigger the draining retry path.
            var exception = new Exception("Function 'TestActivity' failed with an unhandled exception.");

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupActivityTest(exception);

            // Act: should NOT throw — the activity failure flows through to a failed ActivityExecutionResult.
            await middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: the middleware should have set an activity result on the dispatch context.
            ActivityExecutionResult result = dispatchContext.GetProperty<ActivityExecutionResult>();
            Assert.NotNull(result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallOrchestratorAsync_WorkerDrainingResponseWithFailedResult_ThrowsSessionAbortedException()
        {
            // Arrange: the orchestration failed AND the worker flagged the response as produced while
            // draining (shutting down). The host should abort and requeue so it is retried on a healthy
            // worker rather than recording the failure.
            var response = new P.OrchestratorResponse
            {
                InstanceId = "test-instance-id",
                IsWorkerDraining = true,
            };

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupOrchestratorTestWithResponse(EncodeProtobuf(response), succeeded: false);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallOrchestratorAsync_WorkerDrainingResponseWithSucceededResult_DoesNotThrowSessionAbortedException()
        {
            // Arrange: the worker flagged draining but the orchestration still completed successfully.
            // The draining retry only applies to failed results, so a successful result should be committed.
            var response = new P.OrchestratorResponse
            {
                InstanceId = "test-instance-id",
                IsWorkerDraining = true,
            };

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupOrchestratorTestWithResponse(EncodeProtobuf(response), succeeded: true);

            // Act: should NOT throw — a successful result is committed even when the draining flag is set.
            await middleware.CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: the middleware should have set an orchestrator result on the dispatch context.
            OrchestratorExecutionResult result = dispatchContext.GetProperty<OrchestratorExecutionResult>();
            Assert.NotNull(result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallOrchestratorAsync_NotDrainingResponse_DoesNotThrowSessionAbortedException()
        {
            // Arrange: a normal (non-draining) orchestrator response should be committed, not aborted.
            var response = new P.OrchestratorResponse
            {
                InstanceId = "test-instance-id",
                IsWorkerDraining = false,
            };

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupOrchestratorTestWithResponse(EncodeProtobuf(response));

            // Act: should NOT throw — the orchestration result flows through to the dispatch pipeline.
            await middleware.CallOrchestratorAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: the middleware should have set an orchestrator result on the dispatch context.
            OrchestratorExecutionResult result = dispatchContext.GetProperty<OrchestratorExecutionResult>();
            Assert.NotNull(result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_WorkerDrainingResponseWithFailedResult_ThrowsSessionAbortedException()
        {
            // Arrange: the entity batch failed AND the worker flagged the response as produced while
            // draining (shutting down). The host should abort and requeue so it is retried on a healthy
            // worker rather than recording the failure.
            var response = new P.EntityBatchResult
            {
                CompletionToken = "test-completion-token",
                IsWorkerDraining = true,
            };

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupEntityTestWithResponse(EncodeProtobuf(response), succeeded: false);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallEntityAsync(dispatchContext, () => Task.CompletedTask));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_WorkerDrainingResponseWithSucceededResult_DoesNotThrowSessionAbortedException()
        {
            // Arrange: the worker flagged draining but the entity batch still completed successfully.
            // The draining retry only applies to failed results, so a successful result should be committed.
            var response = new P.EntityBatchResult
            {
                CompletionToken = "test-completion-token",
                IsWorkerDraining = true,
            };

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupEntityTestWithResponse(EncodeProtobuf(response), succeeded: true);

            // Act: should NOT throw — a successful result is committed even when the draining flag is set.
            await middleware.CallEntityAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: the middleware should have set an entity batch result on the dispatch context.
            EntityBatchResult result = dispatchContext.GetProperty<EntityBatchResult>();
            Assert.NotNull(result);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CallEntityAsync_NotDrainingResponse_DoesNotThrowSessionAbortedException()
        {
            // Arrange: a normal (non-draining) entity batch result should be committed, not aborted.
            var response = new P.EntityBatchResult
            {
                CompletionToken = "test-completion-token",
                IsWorkerDraining = false,
            };

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) =
                this.SetupEntityTestWithResponse(EncodeProtobuf(response));

            // Act: should NOT throw — the entity result flows through to the dispatch pipeline.
            await middleware.CallEntityAsync(dispatchContext, () => Task.CompletedTask);

            // Assert: the middleware should have set an entity batch result on the dispatch context.
            EntityBatchResult result = dispatchContext.GetProperty<EntityBatchResult>();
            Assert.NotNull(result);
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

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) SetupOrchestratorTestWithResponse(string base64Response, bool succeeded = true)
        {
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext)
                = this.CreateMiddlewareWithInvocation(base64Response, "TestOrchestrator", FunctionType.Orchestrator, succeeded);

            var orchestrationState = new OrchestrationRuntimeState(
                [
                    new ExecutionStartedEvent(-1, null) { Name = "TestOrchestrator" },
                ]);

            dispatchContext.SetProperty(orchestrationState);
            dispatchContext.SetProperty(new OrchestrationInstance { InstanceId = "test-instance-id" });

            return (middleware, dispatchContext);
        }

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) SetupEntityTestWithResponse(string base64Response, bool succeeded = true)
        {
            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext)
                = this.CreateMiddlewareWithInvocation(base64Response, "TestEntity", FunctionType.Entity, succeeded);

            dispatchContext.SetProperty(new EntityBatchRequest
            {
                InstanceId = "@TestEntity@test-key",
                EntityState = null,
                Operations = new List<OperationRequest>(),
            });

            return (middleware, dispatchContext);
        }

        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) CreateMiddleware(
            Exception executorException, string functionName, FunctionType functionType)
        {
            DurableTaskExtension extension = CreateDurableTaskExtension();

            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            mockExecutor
                .Setup(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FunctionResult(false, executorException));

            return (new OutOfProcMiddleware(RegisterAndGetExtension(extension, functionName, functionType, mockExecutor)), CreateDispatchContext(functionType));
        }

        // Creates a middleware that drives the InvokeHandler with the given base64-encoded protobuf
        // response, exercising the host's response-parsing path (including the IsWorkerDraining flag read)
        // the same way a real out-of-proc worker invocation would. The succeeded flag controls whether the
        // resulting FunctionResult reports success or failure, since the worker-draining retry only triggers
        // when the function result is failed.
        private (OutOfProcMiddleware middleware, DispatchMiddlewareContext context) CreateMiddlewareWithInvocation(
            string base64Response, string functionName, FunctionType functionType, bool succeeded = true)
        {
            DurableTaskExtension extension = CreateDurableTaskExtension();

            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            mockExecutor
                .Setup(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Returns<TriggeredFunctionData, CancellationToken>(async (data, ct) =>
                {
                    // The middleware supplies an InvokeHandler that expects the user-code invoker to return
                    // a Task<object> whose result is the base64 protobuf response string.
#pragma warning disable CS0618 // Type or member is obsolete (approved for use by this extension)
                    await data.InvokeHandler(() => Task.FromResult<object>(base64Response));
#pragma warning restore CS0618
                    return succeeded
                        ? new FunctionResult(true)
                        : new FunctionResult(false, new Exception("Function invocation failed."));
                });

            return (new OutOfProcMiddleware(RegisterAndGetExtension(extension, functionName, functionType, mockExecutor)), CreateDispatchContext(functionType));
        }

        private static DurableTaskExtension RegisterAndGetExtension(
            DurableTaskExtension extension, string functionName, FunctionType functionType, Mock<ITriggeredFunctionExecutor> mockExecutor)
        {
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

            return extension;
        }

        private static DispatchMiddlewareContext CreateDispatchContext(FunctionType functionType)
        {
            var dispatchContext = new DispatchMiddlewareContext();

            // Orchestrators and entities require WorkItemMetadata; activities do not.
            if (functionType != FunctionType.Activity)
            {
                dispatchContext.SetProperty(CreateWorkItemMetadata(isExtendedSession: false, includeState: false));
            }

            return dispatchContext;
        }

        private static string EncodeProtobuf(IMessage message)
        {
            return Convert.ToBase64String(message.ToByteArray());
        }

        private static DurableTaskExtension CreateDurableTaskExtension()
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

        private static WorkItemMetadata CreateWorkItemMetadata(bool isExtendedSession, bool includeState)
        {
            // WorkItemMetadata has an internal constructor, so we use reflection to create it.
            ConstructorInfo? ctor = typeof(WorkItemMetadata).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(bool), typeof(bool)],
                modifiers: null);
            Assert.NotNull(ctor);
            return (WorkItemMetadata)ctor.Invoke([isExtendedSession, includeState]);
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
