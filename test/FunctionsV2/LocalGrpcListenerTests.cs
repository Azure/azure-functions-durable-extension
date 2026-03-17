// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

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
        /// TimeoutException (not InvalidOperationException) when the gRPC address is unavailable.
        /// This is critical for queue-triggered functions: TimeoutException signals a transient
        /// issue, preventing rapid poison-queue escalation.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void TestBindingHelper_ThrowsTimeoutException_WhenGrpcAddressUnavailable()
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

        private DurableTaskExtension CreateExtension(string hubName)
        {
            var options = new DurableTaskOptions { HubName = hubName };
            var wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var nameResolver = TestHelpers.GetTestNameResolver();
            var serviceFactory = new AzureStorageDurabilityProviderFactory(
                wrappedOptions,
                new TestStorageServiceClientProviderFactory(),
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService(language: WorkerRuntimeType.DotNetIsolated));

            return new DurableTaskExtension(
                wrappedOptions,
                new LoggerFactory(),
                nameResolver,
                new[] { serviceFactory },
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService(language: WorkerRuntimeType.DotNetIsolated));
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