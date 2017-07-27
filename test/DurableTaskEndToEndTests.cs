// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
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
            using (JobHost host = GetJobHost(nameof(HelloWorldOrchestration_Inline)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(Orchestrations.SayHelloInline), "World", this.output);
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
            using (JobHost host = GetJobHost(nameof(HelloWorldOrchestration_Activity)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(Orchestrations.SayHelloWithActivity), "World", this.output);
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
            using (JobHost host = GetJobHost(nameof(SequentialOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(Orchestrations.Factorial), 10, this.output);
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
            using (JobHost host = GetJobHost(nameof(ParallelOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(Orchestrations.DiskUsage), Environment.CurrentDirectory, this.output);
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
            using (JobHost host = GetJobHost(nameof(ActorOrchestration)))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartFunctionAsync(nameof(Orchestrations.Counter), initialValue, this.output);

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
            using (JobHost host = GetJobHost(nameof(TerminateOrchestration)))
            {
                await host.StartAsync();

                // Using the counter orchestration because it will wait indefinitely for input.
                var client = await host.StartFunctionAsync(nameof(Orchestrations.Counter), 0, this.output);

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
            using (JobHost host = GetJobHost(nameof(TimerCancellation)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartFunctionAsync(nameof(Orchestrations.Approval), timeout, this.output);

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
            using (JobHost host = GetJobHost(nameof(TimerExpiration)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartFunctionAsync(nameof(Orchestrations.Approval), timeout, this.output);

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
            using (JobHost host = GetJobHost(nameof(OrchestrationConcurrency)))
            {
                await host.StartAsync();

                Func<Task> orchestrationStarter = async delegate()
                {
                    var timeout = TimeSpan.FromSeconds(10);
                    var client = await host.StartFunctionAsync(nameof(Orchestrations.Approval), timeout, this.output);
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
            using (JobHost host = GetJobHost(nameof(HandledActivityException)))
            {
                await host.StartAsync();

                // Empty string input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartFunctionAsync(nameof(Orchestrations.TryCatchLoop), 5, this.output);
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
            using (JobHost host = GetJobHost(nameof(UnhandledOrchestrationException)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartFunctionAsync(nameof(Orchestrations.Throw), null, this.output);
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
            using (JobHost host = GetJobHost(nameof(UnhandledActivityException)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartFunctionAsync(nameof(Orchestrations.Throw), message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Exception"));

                await host.StopAsync();
            }
        }

        static JobHost GetJobHost(string taskHub = "CommonTestHub")
        {
            var config = new JobHostConfiguration { HostId = "durable-task-host" };
            config.ConfigureDurableFunctionTypeLocator(typeof(Orchestrations), typeof(Activities));
            config.UseDurableTask(new DurableTaskExtension
            {
                HubName = taskHub.Replace("_", ""),
                TraceInputsAndOutputs = true
            });

            var host = new JobHost(config);
            return host;
        }

        // NOTE: These are not necessarily good real-world use case for durable functions, but help exercise the various
        //       features in potentially realistic ways.
        private static class Orchestrations
        {
            public static void SayHelloInline([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                string input = ctx.GetInput<string>();
                ctx.SetOutput($"Hello, {input}!");
            }

            public static async Task SayHelloWithActivity([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                string input = ctx.GetInput<string>();
                string output = await ctx.CallFunctionAsync<string>(nameof(Activities.Hello), input);
                ctx.SetOutput(output);
            }

            public static async Task Factorial([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                int n = ctx.GetInput<int>();

                long result = 1;
                for (int i = 1; i <= n; i++)
                {
                    result = await ctx.CallFunctionAsync<int>(nameof(Activities.Multiply), new[] { result, i });
                }

                ctx.SetOutput(result);
            }

            public static async Task DiskUsage([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                string directory = ctx.GetInput<string>();
                string[] files = await ctx.CallFunctionAsync<string[]>(nameof(Activities.GetFileList), directory);

                var tasks = new Task<long>[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    tasks[i] = ctx.CallFunctionAsync<long>(nameof(Activities.GetFileSize), files[i]);
                }

                await Task.WhenAll(tasks);

                long totalBytes = tasks.Sum(t => t.Result);
                ctx.SetOutput(totalBytes);
            }

            public static async Task Counter([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                int currentValue = ctx.GetInput<int>();
                string operation = await ctx.WaitForExternalEvent<string>("operation");

                bool done = false;
                switch (operation?.ToLowerInvariant())
                {
                    case "incr":
                        currentValue++;
                        break;
                    case "decr":
                        currentValue--;
                        break;
                    case "end":
                        done = true;
                        break;
                }

                if (done)
                {
                    ctx.SetOutput(currentValue);
                }
                else
                {
                    ctx.ContinueAsNew(currentValue);
                }
            }

            public static async Task Approval([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                TimeSpan timeout = ctx.GetInput<TimeSpan>();
                DateTime deadline = ctx.CurrentUtcDateTime.Add(timeout);

                using (var cts = new CancellationTokenSource())
                {
                    Task<bool> approvalTask = ctx.WaitForExternalEvent<bool>("approval");
                    Task timeoutTask = ctx.CreateTimer(deadline, cts.Token);

                    if (approvalTask == await Task.WhenAny(approvalTask, timeoutTask))
                    {
                        // The timer must be cancelled or fired in order for the orchestration to complete.
                        cts.Cancel();

                        bool approved = approvalTask.Result;
                        ctx.SetOutput(approved ? "Approved" : "Rejected");
                    }
                    else
                    {
                        ctx.SetOutput("Expired");
                    }
                }
            }

            public static async Task Throw([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                string message = ctx.GetInput<string>();
                if (string.IsNullOrEmpty(message))
                {
                    // This throw happens directly in the orchestration.
                    throw new ArgumentNullException(nameof(message));
                }

                // This throw happens in the implementation of an activity.
                await ctx.CallFunctionAsync(nameof(Activities.Throw), message);
            }

            public static async Task TryCatchLoop([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                int iterations = ctx.GetInput<int>();
                int catchCount = 0;

                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        await ctx.CallFunctionAsync(nameof(Activities.Throw), "Kah-BOOOOOM!!!");
                    }
                    catch
                    {
                        catchCount++;
                    }
                }

                ctx.SetOutput(catchCount);
            }

            // TODO: It's not currently possible to detect this failure except by examining logs.
            public static async Task IllegalAwait([OrchestrationTrigger] DurableOrchestrationContext ctx)
            {
                await ctx.CallFunctionAsync(nameof(Activities.Hello), "Foo");

                // This is the illegal await
                await Task.Run(() => { });

                // This call should throw
                await ctx.CallFunctionAsync(nameof(Activities.Hello), "Bar");
            }
        }

        internal static class Activities
        {
            public static void Hello([ActivityTrigger] DurableActivityContext ctx)
            {
                string input = ctx.GetInput<string>();
                ctx.SetOutput($"Hello, {input}!");
            }

            public static void Multiply([ActivityTrigger] DurableActivityContext ctx)
            {
                long[] values = ctx.GetInput<long[]>();
                ctx.SetOutput(values[0] * values[1]);
            }

            public static void GetFileList([ActivityTrigger] DurableActivityContext ctx)
            {
                string directory = ctx.GetInput<string>();
                string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                ctx.SetOutput(files);
            }

            public static void GetFileSize([ActivityTrigger] DurableActivityContext ctx)
            {
                string fileName = ctx.GetInput<string>();
                var info = new FileInfo(fileName);
                ctx.SetOutput(info.Length);
            }

            public static void Throw([ActivityTrigger] DurableActivityContext ctx)
            {
                string message = ctx.GetInput<string>();
                throw new Exception(message);
            }
        }
    }
}
