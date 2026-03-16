// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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