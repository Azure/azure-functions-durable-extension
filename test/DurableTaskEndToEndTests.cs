// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskEndToEndTests
    {
        private readonly ITestOutputHelper output;

        public DurableTaskEndToEndTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// End-to-end test which validates a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Fact]
        public async Task HelloWorldOrchestration_Inline()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(HelloWorldOrchestration_Inline)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.SayHelloInline), "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Fact]
        public async Task HelloWorldOrchestration_Activity()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(HelloWorldOrchestration_Activity)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.SayHelloWithActivity), "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates function chaining by implementing a naive factorial function orchestration.
        /// </summary>
        [Fact]
        public async Task SequentialOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(SequentialOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Factorial), 10, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal(10, status?.Input);
                Assert.Equal(3628800, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates parallel function execution by enumerating all files in the current directory 
        /// in parallel and getting the sum total of all file sizes.
        /// </summary>
        [Fact]
        public async Task ParallelOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(ParallelOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.DiskUsage), Environment.CurrentDirectory, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(90), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal(Environment.CurrentDirectory, status?.Input);
                Assert.True((long?)status?.Output > 0L);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the ContinueAsNew functionality by implementing a counter actor pattern.
        /// </summary>
        [Fact]
        public async Task ActorOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(ActorOrchestration)))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/37
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                // Perform some operations
                await client.RaiseEventAsync("operation", "incr");

                // TODO: Sleeping to avoid a race condition where multiple ContinueAsNew messages
                //       are processed by the same instance at the same time, resulting in a corrupt
                //       storage failure in DTFx.
                // BUG: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/38
                await Task.Delay(2000);
                await client.RaiseEventAsync("operation", "incr");
                await Task.Delay(2000);
                await client.RaiseEventAsync("operation", "incr");
                await Task.Delay(2000);
                await client.RaiseEventAsync("operation", "decr");
                await Task.Delay(2000);
                await client.RaiseEventAsync("operation", "incr");
                await Task.Delay(2000);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.GetStatusAsync();
                Assert.Equal("Running", status?.RuntimeStatus);

                // The end message will cause the actor to complete itself.
                await client.RaiseEventAsync("operation", "end");

                status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal(3, (int)status?.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.NotEqual(initialValue, status?.Input);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the Terminate functionality.
        /// </summary>
        [Fact]
        public async Task TerminateOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(TerminateOrchestration)))
            {
                await host.StartAsync();

                // Using the counter orchestration because it will wait indefinitely for input.
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Counter), 0, this.output);

                // Need to wait for the instance to start before we can terminate it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/37
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                await client.TerminateAsync("sayōnara");

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Terminated", status?.RuntimeStatus);
                Assert.Equal("sayōnara", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the cancellation of durable timers.
        /// </summary>
        [Fact]
        public async Task TimerCancellation()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(TimerCancellation)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Approval), timeout, this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/37
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);
                await client.RaiseEventAsync("approval", eventData: true);

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);
                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal("Approved", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of durable timer expiration.
        /// </summary>
        [Fact]
        public async Task TimerExpiration()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(TimerExpiration)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Approval), timeout, this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/37
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                // Don't send any notification - let the internal timeout expire

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(20), this.output);
                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal("Expired", status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that orchestrations run concurrently of each other (up to 100 by default).
        /// </summary>
        [Fact]
        public async Task OrchestrationConcurrency()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(OrchestrationConcurrency)))
            {
                await host.StartAsync();

                Func<Task> orchestrationStarter = async delegate()
                {
                    var timeout = TimeSpan.FromSeconds(10);
                    var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Approval), timeout, this.output);
                    await client.WaitForCompletionAsync(TimeSpan.FromSeconds(20), this.output);

                    // Don't send any notification - let the internal timeout expire
                };

                int iterations = 100;
                var tasks = new Task[iterations];
                for (int i = 0; i < iterations; i++)
                {
                    tasks[i] = orchestrationStarter();
                }

                // The 100 orchestrations above (which each delay for 10 seconds) should all complete in less than 30 seconds.
                Task parallelOrchestrations = Task.WhenAll(tasks);
                Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));

                Task winner = await Task.WhenAny(parallelOrchestrations, timeoutTask);
                Assert.Equal(parallelOrchestrations, winner);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the orchestrator's exception handling behavior.
        /// </summary>
        [Fact]
        public async Task HandledActivityException()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(HandledActivityException)))
            {
                await host.StartAsync();

                // Empty string input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.TryCatchLoop), 5, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal(5, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from orchestrator code.
        /// </summary>
        [Fact]
        public async Task UnhandledOrchestrationException()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(UnhandledOrchestrationException)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Throw), null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Value cannot be null"));

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from activity code.
        /// </summary>
        [Fact]
        public async Task UnhandledActivityException()
        {
            using (JobHost host = TestHelpers.GetJobHost(nameof(UnhandledActivityException)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Throw), message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Exception"));

                await host.StopAsync();
            }
        }
    }
}
