// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using DurableTask.Core;
using DurableTask.Core.History;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Language.Flow;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using P = Microsoft.DurableTask.Protobuf;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    /// <summary>
    /// Public test enum used to represent LocalGrpcListenerMode values in [Theory] tests,
    /// since the original enum is internal and not directly accessible.
    /// </summary>
    public enum TestGrpcListenerMode
    {
        /// <summary>
        /// Default gRPC listener mode.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Legacy listener mode for backward compatibility.
        /// </summary>
        Legacy = 1,

        /// <summary>
        /// ASP.NET Core-based listener mode.
        /// </summary>
        AspNetCore = 2,
    }

    public class LocalGrpcListenerTests
    {
        private const string TaskHubMetadataKey = "Durable-TaskHub";

        // Host's configured hub for the task hub attribution test. Must be a constant so it can be
        // referenced from [InlineData].
        private const string AttributionDefaultHubName = "AttributionDefaultHub";

        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public LocalGrpcListenerTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Theory]
        [InlineData(TestGrpcListenerMode.Legacy)]
        [InlineData(TestGrpcListenerMode.AspNetCore)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_ShouldStartAndStopSuccessfully(TestGrpcListenerMode testMode)
        {
            // Test boh two version of grpc lisnter mode can start and stop successfully.
            var internalMode = (LocalGrpcListenerMode)(int)testMode;
            await this.GrpcListener_StartAndStopSuccessfully(internalMode);
        }

        // This test covers the methods that have distinct try-catch blocks, and must make sure not to catch the OrchestrationServiceUnavailableException
        // since the gRPC interceptor already handles it
        [Theory]
        [InlineData("StartInstance")]
        [InlineData("RaiseEvent")]
        [InlineData("TerminateInstance")]
        [InlineData("RewindInstance")]
        [InlineData("RestartInstance")]
        [InlineData("PurgeInstances")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_MapsBackendUnavailable(string method)
        {
            const string InstanceId = "test-instance";
            const string Message = "The selected backend is ending migration.";
            var unavailable = new OrchestrationServiceUnavailableException(Message);
            var serviceClient = new Mock<IOrchestrationServiceClient>();
            serviceClient.Setup(c => c.GetOrchestrationStateAsync(InstanceId, It.IsAny<string>())).ThrowsAsync(unavailable);
            serviceClient.Setup(c => c.GetOrchestrationStateAsync(InstanceId, It.IsAny<bool>())).ThrowsAsync(unavailable);
            serviceClient.Setup(c => c.SendTaskOrchestrationMessageAsync(It.IsAny<TaskMessage>())).ThrowsAsync(unavailable);
            Mock<DurabilityProvider> provider = CreateDurabilityProviderMock(new Mock<IOrchestrationService>().Object, serviceClient.Object);
            provider.Setup(p => p.CreateTaskOrchestrationAsync(It.IsAny<TaskMessage>(), It.IsAny<OrchestrationStatus[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(unavailable);
            RpcException error = await this.InvokeFailingRpcAsync("BackendUnavailable", provider.Object, async client =>
            {
                switch (method)
                {
                    case "StartInstance": await client.StartInstanceAsync(new P.CreateInstanceRequest { InstanceId = InstanceId, Name = "Test" }); break;
                    case "RaiseEvent": await client.RaiseEventAsync(new P.RaiseEventRequest { InstanceId = InstanceId, Name = "event" }); break;
                    case "TerminateInstance": await client.TerminateInstanceAsync(new P.TerminateRequest { InstanceId = InstanceId }); break;
                    case "RewindInstance": await client.RewindInstanceAsync(new P.RewindInstanceRequest { InstanceId = InstanceId }); break;
                    case "RestartInstance": await client.RestartInstanceAsync(new P.RestartInstanceRequest { InstanceId = InstanceId }); break;
                    case "PurgeInstances": await client.PurgeInstancesAsync(new P.PurgeInstancesRequest { InstanceId = InstanceId }); break;
                    default: throw new InvalidOperationException(method);
                }
            });
            Assert.Equal(StatusCode.Unavailable, error.StatusCode);
            Assert.Equal(Message, error.Status.Detail);
            Assert.DoesNotContain(this.loggerProvider.GetAllLogMessages(), m => m.Level == LogLevel.Warning && m.FormattedMessage.Contains(Message));
        }

        // Verify the unary gRPC interceptor maps the OrchestrationServiceUnavailableException to status code Unavailable
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_MapsUnavailableDuringWait()
        {
            const string InstanceId = "test-instance";
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ending = new TaskCompletionSource<OrchestrationState>(TaskCreationOptions.RunContinuationsAsynchronously);
            var serviceClient = new Mock<IOrchestrationServiceClient>();
            serviceClient.Setup(c => c.WaitForOrchestrationAsync(InstanceId, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    entered.TrySetResult(true);
                    return ending.Task;
                });
            DurabilityProvider provider = CreateDurabilityProvider(new Mock<IOrchestrationService>().Object, serviceClient.Object);
            RpcException error = await this.InvokeFailingRpcAsync("EndingDuringWait", provider, async client =>
            {
                using AsyncUnaryCall<P.GetInstanceResponse> call = client.WaitForInstanceCompletionAsync(new P.GetInstanceRequest { InstanceId = InstanceId });
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                ending.SetException(new OrchestrationServiceUnavailableException("Migration ended during the wait."));
                await call.ResponseAsync;
            });
            Assert.Equal(StatusCode.Unavailable, error.StatusCode);
        }

        // Verify the streaming gRPC interceptor maps the OrchestrationServiceUnavailableException to status code Unavailable
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_MapsUnavailableDuringHistoryEnumeration()
        {
            const string InstanceId = "test-instance";
            var serviceClient = new Mock<IOrchestrationServiceClient>();
            serviceClient.Setup(c => c.GetOrchestrationStateAsync(InstanceId, It.IsAny<bool>())).ReturnsAsync(
            [
                new ()
                {
                    Name = "Test",
                    CreatedTime = DateTime.UtcNow,
                    LastUpdatedTime = DateTime.UtcNow,
                    OrchestrationInstance = new OrchestrationInstance { InstanceId = InstanceId, ExecutionId = "execution" },
                    OrchestrationStatus = OrchestrationStatus.Running,
                },
            ]);
            Mock<DurabilityProvider> provider = CreateDurabilityProviderMock(new Mock<IOrchestrationService>().Object, serviceClient.Object);
            provider.Setup(p => p.StreamOrchestrationHistoryAsync(InstanceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(EndingHistory());
            RpcException error = await this.InvokeFailingRpcAsync("EndingDuringStream", provider.Object, async client =>
            {
                using AsyncServerStreamingCall<P.HistoryChunk> call = client.StreamInstanceHistory(new P.StreamInstanceHistoryRequest { InstanceId = InstanceId });
                while (await call.ResponseStream.MoveNext(default))
                {
                }
            });
            Assert.Equal(StatusCode.Unavailable, error.StatusCode);
            Assert.Equal("Migration ended during history enumeration.", error.Status.Detail);

            static async IAsyncEnumerable<HistoryEvent> EndingHistory()
            {
                yield return new TaskCompletedEvent(1, 0, "result");
                await Task.Yield();
                throw new OrchestrationServiceUnavailableException("Migration ended during history enumeration.");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_UsesRequestedBackendsAvailability(bool defaultIsEnding)
        {
            // Give the default and requested hubs opposite availability to catch checks against the wrong hub.
            const string InstanceId = "test-instance";
            using DurableTaskExtension source = this.CreateExtension("DefaultHub");
            AzureStorageDurabilityProvider defaultProvider = Assert.IsType<AzureStorageDurabilityProvider>(source.DefaultDurabilityProvider);

            // Set the default hub to ending to confirm that the requested hub (when it is not meant to be ending) is used for the call
            // and does not throw any exceptions
            if (defaultIsEnding)
            {
                object service = typeof(AzureStorageDurabilityProvider).GetField("serviceClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(defaultProvider);
                service.GetType().GetMethod("EnterMigrationEnding", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(service, null);
            }

            var serviceClient = new Mock<IOrchestrationServiceClient>();
            ISetup<IOrchestrationServiceClient, Task<OrchestrationState>> getState = serviceClient.Setup(c => c.GetOrchestrationStateAsync(InstanceId, It.IsAny<string>()));
            if (defaultIsEnding)
            {
                getState.ReturnsAsync((OrchestrationState)null);
            }
            else
            {
                getState.ThrowsAsync(new OrchestrationServiceUnavailableException("Requested hub unavailable."));
            }

            DurabilityProvider requestedProvider = CreateDurabilityProvider(new Mock<IOrchestrationService>().Object, serviceClient.Object);
            using DurableTaskExtension extension = this.CreateExtension("DefaultHub", defaultProvider, attribute =>
            {
                Assert.Equal("RequestedHub", attribute.TaskHub);
                Assert.Equal("RequestedConnection", attribute.ConnectionName);
                return requestedProvider;
            });
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);
            try
            {
                await listener.StartAsync(default);
                using var channel = GrpcChannel.ForAddress(listener.ListenAddress);
                var client = new P.TaskHubSidecarService.TaskHubSidecarServiceClient(channel);
                Assert.NotNull(await client.HelloAsync(new Empty()));

                // Explicitly pass the non-default task hub to the call
                var headers = new Metadata { { "Durable-TaskHub", "RequestedHub" }, { "Durable-ConnectionName", "RequestedConnection" } };
                using AsyncUnaryCall<P.GetInstanceResponse> call = client.GetInstanceAsync(new P.GetInstanceRequest { InstanceId = InstanceId }, headers);

                // If the default task hub is ending, then the requested task hub should not throw the exception.
                // Otherwise if the default task hub is healthy, then the requested task hub should throw the exception.
                if (defaultIsEnding)
                {
                    Assert.False((await call.ResponseAsync).Exists);
                }
                else
                {
                    Assert.Equal(StatusCode.Unavailable, (await Assert.ThrowsAsync<RpcException>(() => call.ResponseAsync)).StatusCode);
                }

                serviceClient.Verify(c => c.GetOrchestrationStateAsync(InstanceId, It.IsAny<string>()), Times.Once);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_LogsUnhandledGetInstanceExceptions()
        {
            const string InstanceId = "test-instance";
            const string ErrorMessage = "The durability provider failed.";

            var providerException = new InvalidOperationException(ErrorMessage);
            DurabilityProvider durabilityProvider = CreateFailingGetStateProvider(InstanceId, providerException);

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                "UnhandledGetInstanceException",
                durabilityProvider,
                async client => await client.GetInstanceAsync(
                    new P.GetInstanceRequest { InstanceId = InstanceId }).ResponseAsync);

            Assert.Equal(StatusCode.Unknown, rpcException.StatusCode);
            this.AssertWarning(ErrorMessage, "GetInstance", nameof(InvalidOperationException));
        }

        [Theory]
        [InlineData("AttributionOtherHub", "AttributionOtherHub")]
        [InlineData(null, AttributionDefaultHubName)]
        [InlineData("", AttributionDefaultHubName)]
        [InlineData("   ", AttributionDefaultHubName)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_AttributesWarningToTaskHubFromRequestHeader(
            string requestTaskHub,
            string expectedHubName)
        {
            const string InstanceId = "test-instance";
            const string ErrorMessage = "The durability provider failed.";

            var providerException = new InvalidOperationException(ErrorMessage);
            DurabilityProvider durabilityProvider = CreateFailingGetStateProvider(InstanceId, providerException);

            Metadata headers = requestTaskHub is null
                ? null
                : new Metadata { { TaskHubMetadataKey, requestTaskHub } };

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                AttributionDefaultHubName,
                durabilityProvider,
                async client => await client.GetInstanceAsync(
                    new P.GetInstanceRequest { InstanceId = InstanceId },
                    headers).ResponseAsync);

            Assert.Equal(StatusCode.Unknown, rpcException.StatusCode);
            LogMessage warning = this.AssertWarning(ErrorMessage, "GetInstance", nameof(InvalidOperationException));
            Assert.Equal(expectedHubName, GetLoggedHubName(warning));
        }

        [Theory]
        [InlineData(true, "create", "CreateTaskHub")]
        [InlineData(false, "delete", "DeleteTaskHub")]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_LogsUnhandledTaskHubManagementExceptions(
            bool createTaskHub,
            string operation,
            string methodName)
        {
            string errorMessage = $"The durability provider failed to {operation} the task hub.";

            var providerException = new InvalidOperationException(errorMessage);
            var orchestrationService = new Mock<IOrchestrationService>();
            Func<P.TaskHubSidecarService.TaskHubSidecarServiceClient, Task> invoke;
            if (createTaskHub)
            {
                orchestrationService
                    .Setup(service => service.CreateAsync(It.IsAny<bool>()))
                    .Returns(Task.FromException(providerException));
                invoke = async client => await client.CreateTaskHubAsync(new P.CreateTaskHubRequest()).ResponseAsync;
            }
            else
            {
                orchestrationService
                    .Setup(service => service.DeleteAsync())
                    .Returns(Task.FromException(providerException));
                invoke = async client => await client.DeleteTaskHubAsync(new P.DeleteTaskHubRequest()).ResponseAsync;
            }

            DurabilityProvider durabilityProvider = CreateDurabilityProvider(
                orchestrationService.Object,
                new Mock<IOrchestrationServiceClient>().Object);

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                $"Unhandled{methodName}Exception",
                durabilityProvider,
                invoke);

            Assert.Equal(StatusCode.Unknown, rpcException.StatusCode);
            this.AssertWarning(errorMessage, methodName, nameof(InvalidOperationException));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_DoesNotLogStartInstanceClientCancellation()
        {
            const string InstanceId = "test-instance";

            var providerCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<DurabilityProvider> durabilityProvider = CreateDurabilityProviderMock(
                new Mock<IOrchestrationService>().Object,
                new Mock<IOrchestrationServiceClient>().Object);
            durabilityProvider
                .Setup(provider => provider.CreateTaskOrchestrationAsync(
                    It.IsAny<TaskMessage>(),
                    It.IsAny<OrchestrationStatus[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(
                    async (
                        TaskMessage message,
                        OrchestrationStatus[] dedupeStatuses,
                        CancellationToken cancellationToken) =>
                    {
                        providerCalled.TrySetResult(true);
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    });

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                "StartInstanceClientCancellation",
                durabilityProvider.Object,
                async client =>
                {
                    using var cancellation = new CancellationTokenSource();
                    using AsyncUnaryCall<P.CreateInstanceResponse> call = client.StartInstanceAsync(
                        new P.CreateInstanceRequest
                        {
                            InstanceId = InstanceId,
                            Name = "TestOrchestration",
                        },
                        cancellationToken: cancellation.Token);

                    await providerCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    cancellation.Cancel();
                    await call.ResponseAsync;
                });

            Assert.Equal(StatusCode.Cancelled, rpcException.StatusCode);
            Assert.DoesNotContain(
                this.loggerProvider.GetAllLogMessages(),
                message => message.Level == LogLevel.Warning &&
                    message.FormattedMessage.Contains("StartInstance"));
        }

        [Theory]
        [InlineData(false, nameof(ApplicationException))]
        [InlineData(true, nameof(OperationCanceledException))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_LogsUnexpectedWrappedRpcExceptions(
            bool providerCanceled,
            string exceptionType)
        {
            const string InstanceId = "test-instance";
            const string Reason = "test-rewind";
            const string ErrorMessage = "The durability provider failed to rewind the instance.";

            Exception providerException = providerCanceled
                ? new OperationCanceledException(ErrorMessage)
                : new ApplicationException(ErrorMessage);
            var orchestrationState = new OrchestrationState
            {
                Name = "TestOrchestration",
                OrchestrationInstance = new OrchestrationInstance
                {
                    ExecutionId = "test-execution",
                    InstanceId = InstanceId,
                },
                OrchestrationStatus = OrchestrationStatus.Failed,
            };
            var orchestrationServiceClient = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClient
                .Setup(client => client.GetOrchestrationStateAsync(InstanceId, (string)null))
                .ReturnsAsync(orchestrationState);
            orchestrationServiceClient
                .Setup(client => client.GetOrchestrationStateAsync(InstanceId, It.IsAny<bool>()))
                .ReturnsAsync(new List<OrchestrationState> { orchestrationState });
            Mock<DurabilityProvider> durabilityProvider = CreateDurabilityProviderMock(
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClient.Object);
            durabilityProvider
                .Setup(provider => provider.RewindAsync(InstanceId, Reason))
                .ThrowsAsync(providerException);

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                "UnexpectedWrappedRpcException",
                durabilityProvider.Object,
                async client => await client.RewindInstanceAsync(
                    new P.RewindInstanceRequest
                    {
                        InstanceId = InstanceId,
                        Reason = Reason,
                    }).ResponseAsync);

            Assert.Equal(StatusCode.Unknown, rpcException.StatusCode);
            Assert.Equal(ErrorMessage, rpcException.Status.Detail);
            this.AssertWarning(ErrorMessage, "RewindInstance", exceptionType);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_PreservesPurgeStatusForProviderCancellation()
        {
            const string InstanceId = "@test-entity";
            const string ErrorMessage = "The durability provider canceled the purge operation.";

            var providerException = new OperationCanceledException(ErrorMessage);
            var orchestrationServiceClient = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClient
                .As<IOrchestrationServicePurgeClient>()
                .Setup(client => client.PurgeInstanceStateAsync(InstanceId))
                .ThrowsAsync(providerException);
            DurabilityProvider durabilityProvider = CreateDurabilityProvider(
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClient.Object);

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                "PurgeProviderCancellation",
                durabilityProvider,
                async client => await client.PurgeInstancesAsync(
                    new P.PurgeInstancesRequest { InstanceId = InstanceId }).ResponseAsync);

            Assert.Equal(StatusCode.Internal, rpcException.StatusCode);
            Assert.Contains(ErrorMessage, rpcException.Status.Detail);
            this.AssertWarning(ErrorMessage, "PurgeInstances", nameof(OperationCanceledException));
        }

        [Theory]
        [InlineData(false, StatusCode.Internal, "The durability provider failed while streaming.", nameof(InvalidOperationException))]
        [InlineData(true, StatusCode.Cancelled, "The durability provider canceled history streaming.", nameof(OperationCanceledException))]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_LogsUnhandledStreamingExceptions(
            bool providerCanceled,
            StatusCode expectedStatus,
            string errorMessage,
            string exceptionType)
        {
            const string InstanceId = "test-instance";

            Exception providerException = providerCanceled
                ? new OperationCanceledException(errorMessage)
                : new InvalidOperationException(errorMessage);
            DurabilityProvider durabilityProvider = CreateFailingStreamHistoryProvider(InstanceId, providerException);

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                "UnhandledStreamingException",
                durabilityProvider,
                async client =>
                {
                    using AsyncServerStreamingCall<P.HistoryChunk> call = client.StreamInstanceHistory(
                        new P.StreamInstanceHistoryRequest { InstanceId = InstanceId });
                    await call.ResponseStream.MoveNext(default);
                });

            Assert.Equal(expectedStatus, rpcException.StatusCode);
            this.AssertWarning(errorMessage, "StreamInstanceHistory", exceptionType);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_DoesNotLogStreamingClientCancellation()
        {
            const string InstanceId = "test-instance";
            const string ErrorMessage = "The response stream was already canceled.";

            var providerCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            DurabilityProvider durabilityProvider = CreateCancelingStreamHistoryProvider(
                InstanceId,
                providerCalled,
                ErrorMessage);

            RpcException rpcException = await this.InvokeFailingRpcAsync(
                "StreamingClientCancellation",
                durabilityProvider,
                async client =>
                {
                    using var cancellation = new CancellationTokenSource();
                    using AsyncServerStreamingCall<P.HistoryChunk> call = client.StreamInstanceHistory(
                        new P.StreamInstanceHistoryRequest { InstanceId = InstanceId },
                        cancellationToken: cancellation.Token);

                    await providerCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    cancellation.Cancel();
                    await call.ResponseStream.MoveNext(default);
                });

            Assert.Equal(StatusCode.Cancelled, rpcException.StatusCode);
            Assert.DoesNotContain(
                this.loggerProvider.GetAllLogMessages(),
                message => message.Level == LogLevel.Warning &&
                    message.FormattedMessage.Contains(ErrorMessage));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_DoesNotLogHandledRpcExceptions()
        {
            const string InstanceId = "missing-instance";

            DurabilityProvider durabilityProvider = CreateEmptyGetStateProvider(InstanceId);
            RpcException rpcException = await this.InvokeFailingRpcAsync(
                "HandledRpcException",
                durabilityProvider,
                async client =>
                {
                    using AsyncServerStreamingCall<P.HistoryChunk> call = client.StreamInstanceHistory(
                        new P.StreamInstanceHistoryRequest { InstanceId = InstanceId });
                    await call.ResponseStream.MoveNext(default);
                });

            Assert.Equal(StatusCode.NotFound, rpcException.StatusCode);
            Assert.DoesNotContain(
                this.loggerProvider.GetAllLogMessages(),
                message => message.Level == LogLevel.Warning &&
                    message.FormattedMessage.Contains("StreamInstanceHistory"));
        }

        [Theory]
        [InlineData(TestGrpcListenerMode.Legacy)]
        [InlineData(TestGrpcListenerMode.AspNetCore)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestMultipleGrpcListeners_ShouldListenToDifferentPorts(TestGrpcListenerMode testMode)
        {
            // Test that multiple gRPC listeners created through the same DurableTaskExtension or host
            // bind to different ports to avoid conflicts.
            var internalMode = (LocalGrpcListenerMode)(int)testMode;
            await this.MultipleGrpcListeners_ShouldListenToDifferentPorts(internalMode);
        }

        // Verifies that the local gRPC listener can start and stop without errors.
        // Also verify the occupied port will be released when stop.
        private async Task GrpcListener_StartAndStopSuccessfully(LocalGrpcListenerMode mode)
        {
            // Create test local grpc listener.
            using DurableTaskExtension extension = this.CreateExtension("GrpcListenerStartAndStopBehavior");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, mode);

            // Verify correct listener type is created
            // (should be AspNetCoreLocalGrpcListener regardless of the mode)
            Assert.IsType<AspNetCoreLocalGrpcListener>(listener);

            try
            {
                await listener.StartAsync(default);

                // Test listen address is valid.
                Assert.NotNull(listener.ListenAddress);
                Assert.True(Uri.TryCreate(listener.ListenAddress, UriKind.Absolute, out Uri uri));
                Assert.True(uri.IsLoopback);
                Assert.Equal("http", uri.Scheme);
                Assert.True(IsPortInUse(uri.Port));

                await listener.StopAsync(default);

                // Assert Port should be released
                await Task.Delay(200); // Give time for cleanup
                Assert.False(IsPortInUse(uri.Port));
            }
            catch
            {
                // Ensure cleanup even if test fails
                await listener.StopAsync(default);
                throw;
            }
        }

        // This task creates two LocalGrpcListener instances using the same extension, simulating a host recycle scenario.
        // E.g., the previous host didn't shut down properly, and a new host was started.
        // Verify that each listener will listen to a different port.
        private async Task MultipleGrpcListeners_ShouldListenToDifferentPorts(LocalGrpcListenerMode mode)
        {
            DurableTaskExtension extension1 = this.CreateExtension("MultipleGrpcListenersListenToDifferentPorts");
            DurableTaskExtension extension2 = this.CreateExtension("MultipleGrpcListenersListenToDifferentPorts");

            ILocalGrpcListener listener1 = LocalGrpcListener.Create(extension1, mode);
            ILocalGrpcListener listener2 = LocalGrpcListener.Create(extension2, mode);

            try
            {
                await listener1.StartAsync(default);
                await listener2.StartAsync(default);

                // Assert
                Assert.NotNull(listener1.ListenAddress);
                Assert.NotNull(listener2.ListenAddress);
                Assert.NotEqual(listener1.ListenAddress, listener2.ListenAddress);

                var uri1 = new Uri(listener1.ListenAddress);
                var uri2 = new Uri(listener2.ListenAddress);

                Assert.NotEqual(uri1.Port, uri2.Port);
                Assert.True(IsPortInUse(uri1.Port));
                Assert.True(IsPortInUse(uri2.Port));
            }
            finally
            {
                // Ensure both listeners are stopped.
                try
                {
                    await listener1.StopAsync(default);
                }
                catch (Exception ex)
                {
                    this.output.WriteLine($"Failed to stop listener1: {ex.Message}");
                }

                try
                {
                    await listener2.StopAsync(default);
                }
                catch (Exception ex)
                {
                    this.output.WriteLine($"Failed to stop listener2: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Verifies that IsHealthy returns false before the listener is started.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TestGrpcListener_IsHealthy_FalseBeforeStart()
        {
            using DurableTaskExtension extension = this.CreateExtension("IsHealthyBeforeStart");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            Assert.False(listener.IsHealthy);
            Assert.Null(listener.ListenAddress);
        }

        /// <summary>
        /// Verifies that IsHealthy returns true after the listener is started
        /// and false again after it is stopped.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_IsHealthy_TrueAfterStart_FalseAfterStop()
        {
            using DurableTaskExtension extension = this.CreateExtension("IsHealthyLifecycle");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                await listener.StartAsync(default);

                Assert.True(listener.IsHealthy);
                Assert.NotNull(listener.ListenAddress);

                await listener.StopAsync(default);

                Assert.False(listener.IsHealthy);
                Assert.Null(listener.ListenAddress);
            }
            catch
            {
                await listener.StopAsync(default);
                throw;
            }
        }

        /// <summary>
        /// Verifies that StopAsync resets the listener state, allowing StartAsync
        /// to restart it on a (potentially different) port.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_CanRestartAfterStop()
        {
            using DurableTaskExtension extension = this.CreateExtension("RestartAfterStop");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                // First start
                await listener.StartAsync(default);
                string firstAddress = listener.ListenAddress;
                Assert.NotNull(firstAddress);
                Assert.True(listener.IsHealthy);

                // Stop — should reset state
                await listener.StopAsync(default);
                Assert.Null(listener.ListenAddress);
                Assert.False(listener.IsHealthy);

                // Restart — should get a new address
                await listener.StartAsync(default);
                Assert.NotNull(listener.ListenAddress);
                Assert.True(listener.IsHealthy);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        /// <summary>
        /// Verifies that EnsureStartedAsync is a no-op when the listener is already healthy.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_EnsureStarted_NoOpWhenHealthy()
        {
            using DurableTaskExtension extension = this.CreateExtension("EnsureStartedNoOp");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                await listener.StartAsync(default);
                string originalAddress = listener.ListenAddress;

                // Calling EnsureStartedAsync when healthy should not change the address
                await listener.EnsureStartedAsync(default);
                Assert.Equal(originalAddress, listener.ListenAddress);
                Assert.True(listener.IsHealthy);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        /// <summary>
        /// Verifies that EnsureStartedAsync restarts the listener when it is not healthy
        /// (e.g. after being stopped).
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_EnsureStarted_RestartsWhenUnhealthy()
        {
            using DurableTaskExtension extension = this.CreateExtension("EnsureStartedRestart");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                await listener.StartAsync(default);
                Assert.True(listener.IsHealthy);

                // Simulate unhealthy state by stopping
                await listener.StopAsync(default);
                Assert.False(listener.IsHealthy);
                Assert.Null(listener.ListenAddress);

                // EnsureStartedAsync should restart
                await listener.EnsureStartedAsync(default);
                Assert.True(listener.IsHealthy);
                Assert.NotNull(listener.ListenAddress);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        /// <summary>
        /// Verifies that WaitForListenAddressAsync returns the address immediately
        /// when the listener is already started.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_WaitForAddress_ReturnsImmediatelyWhenReady()
        {
            using DurableTaskExtension extension = this.CreateExtension("WaitForAddressReady");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                await listener.StartAsync(default);
                string expected = listener.ListenAddress;
                Assert.NotNull(expected);

                // Should return immediately since address is already set
                string result = await listener.WaitForListenAddressAsync(TimeSpan.FromSeconds(5), default);
                Assert.Equal(expected, result);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        /// <summary>
        /// Verifies that WaitForListenAddressAsync returns the address when another
        /// task starts the listener concurrently.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_WaitForAddress_ReturnsWhenStartedConcurrently()
        {
            using DurableTaskExtension extension = this.CreateExtension("WaitForAddressConcurrent");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                // Start waiting before the listener is started
                Task<string> waitTask = listener.WaitForListenAddressAsync(TimeSpan.FromSeconds(10), default);

                // Give a small delay then start the listener
                await Task.Delay(100);
                await listener.StartAsync(default);

                // The wait task should complete with the address
                string result = await waitTask;
                Assert.NotNull(result);
                Assert.Equal(listener.ListenAddress, result);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        /// <summary>
        /// Verifies that WaitForListenAddressAsync returns null when the timeout expires
        /// and the listener was never started.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_WaitForAddress_ReturnsNullOnTimeout()
        {
            using DurableTaskExtension extension = this.CreateExtension("WaitForAddressTimeout");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            // Wait with a very short timeout — listener is never started, so it should time out
            string result = await listener.WaitForListenAddressAsync(TimeSpan.FromMilliseconds(100), default);
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that WaitForListenAddressAsync returns null promptly when
        /// the caller's cancellation token fires.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_WaitForAddress_ReturnsNullOnCancellation()
        {
            using DurableTaskExtension extension = this.CreateExtension("WaitForAddressCancel");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Should return null when the cancellation token fires, not wait the full timeout
            string result = await listener.WaitForListenAddressAsync(TimeSpan.FromSeconds(30), cts.Token);
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that concurrent calls to StartAsync are safe and only one
        /// initialization occurs.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_ConcurrentStartAsync_IsSafe()
        {
            using DurableTaskExtension extension = this.CreateExtension("ConcurrentStart");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                // Start multiple times concurrently
                var tasks = new Task[5];
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = listener.StartAsync(default);
                }

                await Task.WhenAll(tasks);

                // All should succeed, and we should have a valid address
                Assert.NotNull(listener.ListenAddress);
                Assert.True(listener.IsHealthy);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        /// <summary>
        /// Verifies that calling StartAsync again (idempotent) when already started
        /// doesn't change the address.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task TestGrpcListener_StartAsync_IdempotentWhenHealthy()
        {
            using DurableTaskExtension extension = this.CreateExtension("IdempotentStart");
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                await listener.StartAsync(default);
                string firstAddress = listener.ListenAddress;

                // Start again — should be idempotent
                await listener.StartAsync(default);
                Assert.Equal(firstAddress, listener.ListenAddress);
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        /// <summary>
        /// Verifies that GetLocalRpcAddress returns the address after the gRPC listener
        /// is properly started through the extension's EnsureTaskHubWorker path.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TestGetLocalRpcAddress_ReturnsAddressWhenGrpcListenerStarted()
        {
            using DurableTaskExtension extension = this.CreateExtension("GetLocalRpcAddress");

            // The extension is configured for DotNetIsolated (gRPC protocol).
            Assert.Equal(OutOfProcOrchestrationProtocol.MiddlewarePassthrough, extension.OutOfProcProtocol);

            // EnsureTaskHubWorker triggers InitializeTaskHubWorker which starts the gRPC listener
            extension.EnsureTaskHubWorker();

            string address = extension.GetLocalRpcAddress();
            Assert.NotNull(address);
            Assert.True(Uri.TryCreate(address, UriKind.Absolute, out Uri uri));
            Assert.True(uri.IsLoopback);
        }

        /// <summary>
        /// Verifies that BindingHelper.DurableOrchestrationClientToString throws
        /// GrpcChannelTemporarilyUnavailableException (not InvalidOperationException)
        /// when the gRPC address is unavailable.
        /// This is critical for queue-triggered functions: the platform-level exception
        /// signals a transient issue, preventing rapid poison-queue escalation.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TestBindingHelper_ThrowsGrpcChannelUnavailableException_WhenGrpcAddressUnavailable()
        {
            // Create an extension configured for gRPC but with no localGrpcListener instance.
            // Because localGrpcListener is null, GetLocalRpcAddress() skips the
            // EnsureStartedAsync/WaitForListenAddressAsync path entirely and returns null
            // immediately, so this test completes without any 30s wait.
            using DurableTaskExtension extension = this.CreateExtensionWithNullGrpcListener("BindingHelperTimeout");

            var bindingHelper = new BindingHelper(extension);

            // Mock a minimal client
            var attr = new DurableClientAttribute();
            var client = new Moq.Mock<IDurableOrchestrationClient>();
            client.Setup(c => c.TaskHubName).Returns("TestHub");

            // The exception should be GrpcChannelTemporarilyUnavailableException, NOT InvalidOperationException
            var ex = Assert.Throws<GrpcChannelTemporarilyUnavailableException>(
                () => bindingHelper.DurableOrchestrationClientToString(client.Object, attr));

            Assert.Contains("gRPC endpoint", ex.Message);
            Assert.Contains("transient", ex.Message);
        }

        [Theory]
        [InlineData(null, false, false)]
        [InlineData(false, false, false)]
        [InlineData(true, true, false)]
        [InlineData(null, false, true)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TestBindingHelper_SerializesUseForwardedHost(
            bool? configuredValue,
            bool expectedValue,
            bool nullHttpSettings)
        {
            var options = new DurableTaskOptions
            {
                HubName = "UseForwardedHost",
                WebhookUriProviderOverride = () => new Uri("https://durable.test/runtime/webhooks/durabletask"),
            };
            if (nullHttpSettings)
            {
                options.HttpSettings = null;
            }
            else if (configuredValue.HasValue)
            {
                options.HttpSettings.UseForwardedHost = configuredValue.Value;
            }

            using DurableTaskExtension extension = this.CreateExtension(options, WorkerRuntimeType.DotNetIsolated);
            extension.EnsureTaskHubWorker();

            var bindingHelper = new BindingHelper(extension);
            var client = new Mock<IDurableOrchestrationClient>();
            client.Setup(c => c.TaskHubName).Returns(options.HubName);

            string payload = bindingHelper.DurableOrchestrationClientToString(
                client.Object,
                new DurableClientAttribute());

            Assert.Equal(expectedValue, JObject.Parse(payload).Value<bool>("useForwardedHost"));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TestBindingHelper_OmitsUseForwardedHostFromLegacyPayload()
        {
            var options = new DurableTaskOptions
            {
                HubName = "LegacyUseForwardedHost",
                WebhookUriProviderOverride = () => new Uri("https://durable.test/runtime/webhooks/durabletask"),
            };
            options.HttpSettings.UseForwardedHost = true;
            using DurableTaskExtension extension = this.CreateExtension(options, WorkerRuntimeType.DotNet);

            var bindingHelper = new BindingHelper(extension);
            var client = new Mock<IDurableOrchestrationClient>();
            client.Setup(c => c.TaskHubName).Returns(options.HubName);

            string payload = bindingHelper.DurableOrchestrationClientToString(
                client.Object,
                new DurableClientAttribute());

            Assert.False(JObject.Parse(payload).ContainsKey("useForwardedHost"));
        }

        /// <summary>
        /// Verifies that the native worker runtimes (the Go worker reports either "native" or,
        /// defensively, "golang") select the gRPC protocol (MiddlewarePassthrough) at extension
        /// initialization rather than the legacy HTTP-correlation shim (OrchestratorShim). When
        /// MiddlewarePassthrough is selected the local HTTP RPC server is never started — the
        /// runtime communicates durable operations over the local gRPC sidecar instead.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(WorkerRuntimeType.Native)]
        [InlineData(WorkerRuntimeType.Golang)]
        public void TestNativeRuntime_SelectsGrpcProtocol(WorkerRuntimeType runtimeType)
        {
            using DurableTaskExtension extension = this.CreateExtension("NativeRuntimeProtocol", runtimeType);

            Assert.Equal(OutOfProcOrchestrationProtocol.MiddlewarePassthrough, extension.OutOfProcProtocol);
            Assert.NotEqual(OutOfProcOrchestrationProtocol.OrchestratorShim, extension.OutOfProcProtocol);
        }

        private DurableTaskExtension CreateExtension(string hubName)
        {
            return this.CreateExtension(hubName, WorkerRuntimeType.DotNetIsolated);
        }

        private async Task<RpcException> InvokeFailingRpcAsync(
            string hubName,
            DurabilityProvider durabilityProvider,
            Func<P.TaskHubSidecarService.TaskHubSidecarServiceClient, Task> invoke)
        {
            using DurableTaskExtension extension = this.CreateExtension(hubName, durabilityProvider);
            ILocalGrpcListener listener = LocalGrpcListener.Create(extension, LocalGrpcListenerMode.AspNetCore);

            try
            {
                await listener.StartAsync(default);
                using GrpcChannel channel = GrpcChannel.ForAddress(listener.ListenAddress);
                var client = new P.TaskHubSidecarService.TaskHubSidecarServiceClient(channel);
                return await Assert.ThrowsAsync<RpcException>(() => invoke(client));
            }
            finally
            {
                await listener.StopAsync(default);
            }
        }

        private LogMessage AssertWarning(string errorMessage, string methodName, string exceptionType)
        {
            LogMessage warning = Assert.Single(
                this.loggerProvider.GetAllLogMessages(),
                message => message.Level == LogLevel.Warning &&
                    message.FormattedMessage.Contains(errorMessage));
            Assert.Contains(methodName, warning.FormattedMessage);
            Assert.Contains(exceptionType, warning.FormattedMessage);
            return warning;
        }

        private static string GetLoggedHubName(LogMessage warning)
        {
            return warning.State.Single(pair => pair.Key == "hubName").Value?.ToString();
        }

        private DurableTaskExtension CreateExtension(string hubName, WorkerRuntimeType runtimeType)
        {
            return this.CreateExtension(new DurableTaskOptions { HubName = hubName }, runtimeType);
        }

        private DurableTaskExtension CreateExtension(
            DurableTaskOptions options,
            WorkerRuntimeType runtimeType,
            INameResolver nameResolver = null)
        {
            var wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            nameResolver ??= TestHelpers.GetTestNameResolver();
            var serviceFactory = new AzureStorageDurabilityProviderFactory(
                wrappedOptions,
                new TestStorageServiceClientProviderFactory(),
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(language: runtimeType));

            return new DurableTaskExtension(
                wrappedOptions,
                new LoggerFactory(),
                nameResolver,
                new[] { serviceFactory },
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService(language: runtimeType));
        }

        private DurableTaskExtension CreateExtension(string hubName, DurabilityProvider durabilityProvider, Func<DurableClientAttribute, DurabilityProvider> selectProvider = null)
        {
            var options = new DurableTaskOptions { HubName = hubName };
            var wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var serviceFactory = new Mock<IDurabilityProviderFactory>();
            serviceFactory.SetupGet(factory => factory.Name).Returns(AzureStorageDurabilityProviderFactory.ProviderName);
            serviceFactory.Setup(factory => factory.GetDurabilityProvider()).Returns(durabilityProvider);
            serviceFactory
                .Setup(factory => factory.GetDurabilityProvider(It.IsAny<DurableClientAttribute>()))
                .Returns((DurableClientAttribute attribute) => selectProvider?.Invoke(attribute) ?? durabilityProvider);

            var loggerFactory = new LoggerFactory(new[] { this.loggerProvider });
            return new DurableTaskExtension(
                wrappedOptions,
                loggerFactory,
                TestHelpers.GetTestNameResolver(),
                new[] { serviceFactory.Object },
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService(
                    language: WorkerRuntimeType.DotNetIsolated));
        }

        private static DurabilityProvider CreateFailingGetStateProvider(string instanceId, Exception exception)
        {
            var orchestrationServiceClient = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClient
                .Setup(client => client.GetOrchestrationStateAsync(instanceId, It.IsAny<string>()))
                .ThrowsAsync(exception);
            orchestrationServiceClient
                .Setup(client => client.GetOrchestrationStateAsync(instanceId, It.IsAny<bool>()))
                .ThrowsAsync(exception);

            return CreateDurabilityProvider(
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClient.Object);
        }

        private static DurabilityProvider CreateEmptyGetStateProvider(string instanceId)
        {
            var orchestrationServiceClient = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClient
                .Setup(client => client.GetOrchestrationStateAsync(instanceId, It.IsAny<bool>()))
                .ReturnsAsync(new List<OrchestrationState>());

            return CreateDurabilityProvider(
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClient.Object);
        }

        private static DurabilityProvider CreateFailingStreamHistoryProvider(string instanceId, Exception exception)
        {
            var orchestrationServiceClient = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClient
                .Setup(client => client.GetOrchestrationStateAsync(instanceId, It.IsAny<bool>()))
                .ReturnsAsync(new List<OrchestrationState>
                {
                    new OrchestrationState
                    {
                        CreatedTime = DateTime.UtcNow,
                        LastUpdatedTime = DateTime.UtcNow,
                        Name = "TestOrchestration",
                        OrchestrationInstance = new OrchestrationInstance
                        {
                            ExecutionId = "test-execution",
                            InstanceId = instanceId,
                        },
                        OrchestrationStatus = OrchestrationStatus.Running,
                    },
                });

            Mock<DurabilityProvider> durabilityProvider = CreateDurabilityProviderMock(
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClient.Object);
            durabilityProvider
                .Setup(provider => provider.StreamOrchestrationHistoryAsync(
                    instanceId,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            return durabilityProvider.Object;
        }

        private static DurabilityProvider CreateCancelingStreamHistoryProvider(
            string instanceId,
            TaskCompletionSource<bool> providerCalled,
            string errorMessage)
        {
            var orchestrationServiceClient = new Mock<IOrchestrationServiceClient>();
            orchestrationServiceClient
                .Setup(client => client.GetOrchestrationStateAsync(instanceId, It.IsAny<bool>()))
                .ReturnsAsync(new List<OrchestrationState>
                {
                    new OrchestrationState
                    {
                        CreatedTime = DateTime.UtcNow,
                        LastUpdatedTime = DateTime.UtcNow,
                        Name = "TestOrchestration",
                        OrchestrationInstance = new OrchestrationInstance
                        {
                            ExecutionId = "test-execution",
                            InstanceId = instanceId,
                        },
                        OrchestrationStatus = OrchestrationStatus.Running,
                    },
                });

            Mock<DurabilityProvider> durabilityProvider = CreateDurabilityProviderMock(
                new Mock<IOrchestrationService>().Object,
                orchestrationServiceClient.Object);
            durabilityProvider
                .Setup(provider => provider.StreamOrchestrationHistoryAsync(
                    instanceId,
                    It.IsAny<CancellationToken>()))
                .Returns(
                    (string requestedInstanceId, CancellationToken cancellationToken) =>
                        Task.FromResult<IAsyncEnumerable<HistoryEvent>>(
                            ThrowAfterCancellationAsync(providerCalled, errorMessage, cancellationToken)));

            return durabilityProvider.Object;
        }

        private static async IAsyncEnumerable<HistoryEvent> ThrowAfterCancellationAsync(
            TaskCompletionSource<bool> providerCalled,
            string errorMessage,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            providerCalled.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(errorMessage);
            }

            yield break;
        }

        private static DurabilityProvider CreateDurabilityProvider(
            IOrchestrationService orchestrationService,
            IOrchestrationServiceClient orchestrationServiceClient)
        {
            return CreateDurabilityProviderMock(orchestrationService, orchestrationServiceClient).Object;
        }

        private static Mock<DurabilityProvider> CreateDurabilityProviderMock(
            IOrchestrationService orchestrationService,
            IOrchestrationServiceClient orchestrationServiceClient)
        {
            var durabilityProvider = new Mock<DurabilityProvider>(
                "Test",
                orchestrationService,
                orchestrationServiceClient,
                "TestConnection")
            {
                CallBase = true,
            };
            durabilityProvider
                .Setup(provider => provider.SetUseSeparateQueueForEntityWorkItems(It.IsAny<bool>()));

            return durabilityProvider;
        }

        /// <summary>
        /// Creates an extension configured for gRPC protocol (MiddlewarePassthrough)
        /// but WITHOUT a gRPC listener, simulating a state where the listener was never
        /// created or has been lost. This is used to test error handling paths.
        /// </summary>
        private DurableTaskExtension CreateExtensionWithNullGrpcListener(string hubName)
        {
            var options = new DurableTaskOptions { HubName = hubName };
            var wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var nameResolver = TestHelpers.GetTestNameResolver();
            var serviceFactory = new AzureStorageDurabilityProviderFactory(
                wrappedOptions,
                new TestStorageServiceClientProviderFactory(),
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(language: WorkerRuntimeType.DotNet));

            // Create extension with DotNet (in-process) runtime so it doesn't auto-configure gRPC,
            // then manually set the protocol to MiddlewarePassthrough with no listener.
            var extension = new DurableTaskExtension(
                wrappedOptions,
                new LoggerFactory(),
                nameResolver,
                new[] { serviceFactory },
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService(language: WorkerRuntimeType.DotNet));

            // Force gRPC protocol mode without a listener to simulate broken state
            extension.OutOfProcProtocol = OutOfProcOrchestrationProtocol.MiddlewarePassthrough;

            return extension;
        }

        private static bool IsPortInUse(int port)
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                tcpListener.Start();
                return false; // Port is not in use
            }
            catch (SocketException)
            {
                return true; // Port is in use
            }
            finally
            {
                tcpListener.Stop();
            }
        }
    }
}
