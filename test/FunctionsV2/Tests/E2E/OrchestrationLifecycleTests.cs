// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class OrchestrationLifecycleTests : DurableTaskEndToEndTestBase
    {
        public OrchestrationLifecycleTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActorOrchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActorOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);

                // Wait for the instance to go into the Running state. This is necessary to ensure log validation consistency.
                await client.WaitForStartupAsync(this.output);

                TimeSpan waitTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 300 : 5);

                // Perform some operations
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 1);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 3);
                await client.RaiseEventAsync("operation", "decr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 2);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 3);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.GetStatusAsync();
                Assert.NotNull(status);
                Assert.True(
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                // The end message will cause the actor to complete itself.
                await client.RaiseEventAsync("operation", "end", this.output);

                status = await client.WaitForCompletionAsync(this.output, timeout: waitTimeout);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(3, (int?)status.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.Equal(3, (int)status.Input);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        nameof(this.ActorOrchestration),
                        client.InstanceId,
                        extendedSessions,
                        new[] { nameof(TestOrchestrations.Counter) });
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the ContinueAsNew functionality by implementing a counter actor pattern,
        /// and does so without any waiting between sending events.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ActorOrchestration_NoWaiting(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActorOrchestration_NoWaiting),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "decr", this.output);
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "end", this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(3, (int?)status.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.Equal(3, (int)status.Input);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BatchedActorOrchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.BatchedActorOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.BatchActor), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("newItem", "item1", this.output);
                await client.RaiseEventAsync("newItem", "item2", this.output);
                await client.RaiseEventAsync("newItem", "item3", this.output);
                await client.RaiseEventAsync("newItem", "item4", this.output);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.WaitForStartupAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

                // Sending this last item will cause the actor to complete itself.
                await client.RaiseEventAsync("newItem", "item5", this.output);

                status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task BatchedActorOrchestrationDeleteLastItemAlways(bool extendedSessions, string storageProvider)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.BatchedActorOrchestrationDeleteLastItemAlways),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.BatchActorRemoveLast), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item5
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item4
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item3
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item2

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.WaitForStartupAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

                // Sending this last event will cause the actor to complete itself.
                await client.RaiseEventAsync("deleteItem", this.output); // deletes last item in the list: item1

                status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the parallel wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ParallelBatchedActorOrchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ParallelBatchedActorOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ParallelBatchActor), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("newItem", "item1", this.output);
                await client.RaiseEventAsync("newItem", "item2", this.output);
                await client.RaiseEventAsync("newItem", "item3", this.output);

                // Make sure it's still running and didn't complete early (or fail).
                await client.WaitForStartupAsync(this.output);
                await Task.Delay(TimeSpan.FromSeconds(2));
                var status = await client.GetStatusAsync();
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

                // Sending this last item will cause the actor to complete itself.
                await client.RaiseEventAsync("newItem", "item4", this.output);
                status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ExternalEvents_MultipleNamesLooping(bool extendedSessions, string storageProvider)
        {
            const string testName = nameof(this.ExternalEvents_MultipleNamesLooping);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter2), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("incr", null, this.output);
                await client.RaiseEventAsync("incr", null, this.output);
                await client.RaiseEventAsync("done", null, this.output);

                // Make sure it actually completed
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(2, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the Terminate functionality.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task TerminateOrchestration(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Counter),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TerminateOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                // Using the counter orchestration because it will wait indefinitely for input.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], 0, this.output);

                // Need to wait for the instance to start before we can terminate it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(this.output);

                await client.TerminateAsync("sayōnara");

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Terminated, status.RuntimeStatus);
                Assert.Equal("sayōnara", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "TerminateOrchestration",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the Suspend-Resume functionality.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SuspendResumeOrchestration(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Counter),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.SuspendResumeOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], 0, this.output);
                await client.WaitForStartupAsync(this.output);

                // Test case 1: Suspend changes the status Running->Suspended
                await client.SuspendAsync("sleepyOrch");
                var status = await client.WaitForStatusChange(this.output, OrchestrationRuntimeStatus.Suspended);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Suspended, status.RuntimeStatus);

                // Test case 2: external event does not go through
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.RaiseEventAsync("operation", "end", this.output);
                status = await client.GetStatusAsync(showInput: false);
                Assert.NotNull(status);
                Assert.Equal(0, status.Output);

                // Test case 3: external event now goes through
                await client.ResumeAsync("wakeUp");
                status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(1, status.Output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the Rewind functionality.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task RewindOrchestration(string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityForRewind),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.RewindOrchestration),
                enableExtendedSessions: false,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "Catherine", this.output);

                await client.WaitForStartupAsync(this.output);

                var statusFail = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, statusFail?.RuntimeStatus);

                TestOrchestrations.SayHelloWithActivityForRewindShouldFail = false;

                await client.RewindAsync("rewind!");

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Hello, Catherine!", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "RewindOrchestration",
                        client.InstanceId,
                        false /* filterOutReplayLogs */,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the cancellation of durable timers.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task TimerCancellation(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TimerCancellation),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);
                await client.WaitForStartupAsync(this.output);
                await client.RaiseEventAsync("approval", eventData: true, output: this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Approved", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "TimerCancellation",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of durable timer expiration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task TimerExpiration(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TimerExpiration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(2);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);
                await client.WaitForStartupAsync(this.output);

                // Don't send any notification - let the internal timeout expire

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Expired", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "TimerExpiration",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the overloads of WaitForExternalEvent with timeout.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData("throw", false, "TimeoutException")]
        [InlineData("throw", true, "ApprovalValue")]
        [InlineData("default", true, "ApprovalValue")]
        [InlineData("default", false, "default")]
        public async Task WaitForExternalEventWithTimeout(string defaultValue, bool sendEvent, string expectedResponse)
        {
            var orchestratorFunctionNames = new[] { nameof(TestOrchestrations.ApprovalWithTimeout) };
            var extendedSessions = false;
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.WaitForExternalEventWithTimeout),
                extendedSessions))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], (timeout, defaultValue), this.output);
                await client.WaitForStartupAsync(this.output);

                // Don't send any notification - let the internal timeout expire
                if (sendEvent)
                {
                    await client.RaiseEventAsync("Approval", "ApprovalValue", this.output);
                }

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(expectedResponse, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates a CancellationToken-providing overload of WaitForExternalEvent.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task WaitForExternalEventWithCancellationToken()
        {
            var orchestratorFunctionNames = new[] { nameof(TestOrchestrations.ApprovalWithCancellationToken) };
            var extendedSessions = false;
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.WaitForExternalEventWithCancellationToken),
                extendedSessions))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(30);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);
                await client.WaitForStartupAsync(this.output);

                await client.RaiseEventAsync("approval", this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.InRange(status.LastUpdatedTime - status.CreatedTime, TimeSpan.Zero, timeout);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that orchestrations run concurrently of each other (up to 100 by default).
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task OrchestrationConcurrency(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationConcurrency),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                Func<Task> orchestrationStarter = async () =>
                {
                    var timeout = TimeSpan.FromSeconds(3);
                    var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Approval), timeout, this.output);
                    await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(30));

                    // Don't send any notification - let the internal timeout expire
                };

                int iterations = 30;
                var tasks = new Task[iterations];
                for (int i = 0; i < iterations; i++)
                {
                    tasks[i] = orchestrationStarter();
                }

                // The 30 orchestrations above (which each delay for 3 seconds) should all complete in less than 90 seconds.
                Task parallelOrchestrations = Task.WhenAll(tasks);
                Task timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));

                Task winner = await Task.WhenAny(parallelOrchestrations, timeoutTask);
                Assert.Equal(parallelOrchestrations, winner);

                await host.StopAsync();
            }
        }
    }
}
