// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.Exceptions;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OutOfProcMiddlewareTests
    {
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
        public async Task CallActivityAsync_FunctionTimeoutAbortException_ThrowsSessionAbortedException()
        {
            var exception = new FunctionTimeoutAbortException("Activity A timed out! Worker channel closing");

            (OutOfProcMiddleware middleware, DispatchMiddlewareContext dispatchContext) = this.SetupActivityTest(exception);

            await Assert.ThrowsAsync<SessionAbortedException>(
                () => middleware.CallActivityAsync(dispatchContext, () => Task.CompletedTask));
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

            // GrpcChannelTemporarilyUnavailableException as InnerException (gRPC sidecar unavailable)
            yield return new object[] { new Exception("Function invocation failed.", new GrpcChannelTemporarilyUnavailableException("The local gRPC endpoint is not available.")) };
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
            ConstructorInfo ctor = typeof(WorkItemMetadata).GetConstructor(
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
