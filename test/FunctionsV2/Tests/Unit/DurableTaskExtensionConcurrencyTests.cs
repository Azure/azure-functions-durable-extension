// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Grpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskExtensionConcurrencyTests
    {
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public void EnsureTaskHubWorker_ConcurrentCalls_ReturnsSameInstance()
        {
            // Arrange
            using DurableTaskExtension extension = CreateExtension("ConcurrencyTest");
            int threadCount = 10;
            var results = new ConcurrentBag<TaskHubWorker>();
            using var barrier = new ManualResetEventSlim(false);

            // Act
            var threads = Enumerable.Range(0, threadCount).Select(_ => new Thread(() =>
            {
                barrier.Wait();
                TaskHubWorker worker = extension.EnsureTaskHubWorker();
                results.Add(worker);
            })).ToArray();

            foreach (var thread in threads)
            {
                thread.Start();
            }

            barrier.Set();

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Equal(threadCount, results.Count);
            TaskHubWorker first = results.First();
            Assert.All(results, worker => Assert.Same(first, worker));
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task AspNetCoreLocalGrpcListener_ConcurrentStartAsync_StartsOnlyOnce()
        {
            // Arrange
            using DurableTaskExtension extension = CreateExtension("GrpcConcurrencyTest");
            var listener = new AspNetCoreLocalGrpcListener(extension);
            int taskCount = 10;

            // Act
            var listenAddresses = new ConcurrentBag<string?>();
            var tasks = Enumerable.Range(0, taskCount)
                .Select(async _ =>
                {
                    await listener.StartAsync(default);

                    // Each caller should see a valid ListenAddress after StartAsync returns
                    listenAddresses.Add(listener.ListenAddress);
                })
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert: every concurrent caller observed a valid address after StartAsync returned
            Assert.Equal(taskCount, listenAddresses.Count);
            Assert.All(listenAddresses, addr =>
            {
                Assert.NotNull(addr);
                Assert.True(Uri.TryCreate(addr, UriKind.Absolute, out Uri? uri));
                Assert.True(uri!.IsLoopback);
            });

            // Cleanup
            await listener.StopAsync(default);
        }

        private static DurableTaskExtension CreateExtension(string hubName)
        {
            var options = new DurableTaskOptions { HubName = hubName };
            var wrappedOptions = new OptionsWrapper<DurableTaskOptions>(options);
            var nameResolver = TestHelpers.GetTestNameResolver();
            var serviceFactory = new AzureStorageDurabilityProviderFactory(
                wrappedOptions,
                new TestStorageServiceClientProviderFactory(),
                nameResolver,
                NullLoggerFactory.Instance,
                TestHelpers.GetMockPlatformInformationService());

            return new DurableTaskExtension(
                wrappedOptions,
                new LoggerFactory(),
                nameResolver,
                new[] { serviceFactory },
                new TestHostShutdownNotificationService(),
                new DurableHttpMessageHandlerFactory(),
                platformInformationService: TestHelpers.GetMockPlatformInformationService());
        }
    }
}
