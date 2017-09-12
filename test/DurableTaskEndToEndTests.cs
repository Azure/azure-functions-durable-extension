// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskEndToEndTests
    {
        private readonly ITestOutputHelper output;

        private readonly ILoggerFactory loggerFactory;
        private readonly TestLoggerProvider loggerProvider;
        private readonly bool useTestLogger;

        private readonly string instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

        public DurableTaskEndToEndTests(ITestOutputHelper output)
        {
            this.output = output;

            // Set to false to manually verify log entries in Application Insights but tests with TestHelpers.AssertLogMessageSequence will be skipped
            this.useTestLogger = true;

            loggerProvider = new TestLoggerProvider();
            loggerFactory = new LoggerFactory();

            if (useTestLogger)
            {
                loggerFactory.AddProvider(loggerProvider);
            }
            else
            {
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    var filter = new LogCategoryFilter
                    {
                        DefaultLevel = LogLevel.Debug
                    };

                    filter.CategoryLevels[TestHelpers.LogCategory] = LogLevel.Debug;

                    loggerFactory = new LoggerFactory()
                        .AddApplicationInsights(instrumentationKey, filter.Filter);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Fact]
        public async Task HelloWorldOrchestration_Inline()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline)
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(HelloWorldOrchestration_Inline)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "HelloWorldOrchestration_Inline",
                    orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which runs a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Fact]
        public async Task HelloWorldOrchestration_Activity()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity)
            };
            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(HelloWorldOrchestration_Activity)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "HelloWorldOrchestration_Activity",
                    orchestratorFunctionNames, activityFunctionName);
            }
        }

        /// <summary>
        /// End-to-end test which validates function chaining by implementing a naive factorial function orchestration.
        /// </summary>
        [Fact]
        public async Task SequentialOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(SequentialOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Factorial), 10, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Completed", status?.RuntimeStatus);
                Assert.Equal(10, status?.Input);
                Assert.Equal(3628800, status?.Output);

                await host.StopAsync();
            }

            // Assert log entry count
            if (this.useTestLogger)
            {
                var logger = loggerProvider.CreatedLoggers.Single(l => l.Category == TestHelpers.LogCategory);
                var logMessages = logger.LogMessages.ToList();
                Assert.Equal(153, logMessages.Count);
            }
        }
        
        
        /// <summary>
        /// End-to-end test which validates parallel function execution by enumerating all files in the current directory 
        /// in parallel and getting the sum total of all file sizes.
        /// </summary>
        [Fact]
        public async Task ParallelOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(ParallelOrchestration)))
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
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(ActorOrchestration)))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Counter), initialValue,
                    this.output);

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
                Assert.Equal(3, (int) status?.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.NotEqual(initialValue, status?.Input);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                var logger = loggerProvider.CreatedLoggers.Single(l => l.Category == TestHelpers.LogCategory);
                var logMessages = logger.LogMessages.ToList();
                Assert.Equal(49, logMessages.Count);
            }
        }

        /// <summary>
        /// End-to-end test which validates the Terminate functionality.
        /// </summary>
        [Fact]
        public async Task TerminateOrchestration()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Counter)
            };
            
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(TerminateOrchestration)))
            {
                await host.StartAsync();

                // Using the counter orchestration because it will wait indefinitely for input.
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], 0, this.output);

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

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "TerminateOrchestration",
                    orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which validates the cancellation of durable timers.
        /// </summary>
        [Fact]
        public async Task TimerCancellation()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval)
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(TimerCancellation)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], timeout, this.output);

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

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "TimerCancellation", orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of durable timer expiration.
        /// </summary>
        [Fact]
        public async Task TimerExpiration()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval)
            };
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(TimerExpiration)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], timeout, this.output);

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

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "TimerExpiration", orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which validates that orchestrations run concurrently of each other (up to 100 by default).
        /// </summary>
        [Fact]
        public async Task OrchestrationConcurrency()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(OrchestrationConcurrency)))
            {
                await host.StartAsync();

                Func<Task> orchestrationStarter = async delegate()
                {
                    var timeout = TimeSpan.FromSeconds(10);
                    var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Approval), timeout, this.output);
                    await client.WaitForCompletionAsync(TimeSpan.FromSeconds(60), this.output);

                    // Don't send any notification - let the internal timeout expire
                };

                int iterations = 100;
                var tasks = new Task[iterations];
                for (int i = 0; i < iterations; i++)
                {
                    tasks[i] = orchestrationStarter();
                }

                // The 100 orchestrations above (which each delay for 10 seconds) should all complete in less than 40 seconds.
                Task parallelOrchestrations = Task.WhenAll(tasks);
                Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(40));

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
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(HandledActivityException)))
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
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Throw)
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(UnhandledOrchestrationException)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Value cannot be null"));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "UnhandledOrchestrationException",
                    orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which validates calling an orchestrator function.
        /// </summary>
        [Fact]
        public async Task Orchestration_Activity()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorGreeting),
                nameof(TestOrchestrations.SayHelloWithActivity)
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(Orchestration_Activity)))
            {
                await host.StartAsync();

                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(20), this.output);

                Assert.NotNull(status);
                Assert.Equal("Completed", status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(orchestratorFunctionNames[0], status?.Name);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "Orchestration_Activity",
                    orchestratorFunctionNames, activityFunctionName);
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from orchestrator functions.
        /// </summary>
        [Fact]
        public async Task UnhandledOrchestrationExceptionWithRetry()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorThrowWithRetry)
            };

            using (JobHost host =
                TestHelpers.GetJobHost(loggerFactory, nameof(UnhandledOrchestrationExceptionWithRetry)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(50), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Value cannot be null"));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(loggerProvider, "UnhandledOrchestrationExceptionWithRetry",
                    orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an orchestrator function with null RetryOptions fails.
        /// </summary>
        [Fact]
        public async Task OrchestrationWithRetry_NullRetryOptions()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorWithRetry_NullRetryOptions)
            };

            using (JobHost host =
                TestHelpers.GetJobHost(loggerFactory, nameof(OrchestrationWithRetry_NullRetryOptions)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(50), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Value cannot be null.\r\nParameter name: retryOptions"));

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from activity code.
        /// </summary>
        [Fact]
        public async Task UnhandledActivityException()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Throw)
            };
            string activityFunctionName = nameof(TestActivities.Throw);

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(UnhandledActivityException)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Exception"));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "UnhandledActivityException",
                    orchestratorFunctionNames, activityFunctionName);
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from activity functions.
        /// </summary>
        [Fact]
        public async Task UnhandledActivityExceptionWithRetry()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ActivityThrowWithRetry)
            };
            string activityFunctionName = nameof(TestActivities.Throw);

            using (JobHost host = TestHelpers.GetJobHost(loggerFactory, nameof(UnhandledActivityExceptionWithRetry)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(40), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Exception"));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "UnhandledActivityExceptionWithRetry",
                    orchestratorFunctionNames, activityFunctionName);
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an activity function with null RetryOptions fails.
        /// </summary>
        [Fact]
        public async Task ActivityWithRetry_NullRetryOptions()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ActivityWithRetry_NullRetryOptions)
            };
            string activityFunctionName = nameof(TestActivities.Throw);

            using (JobHost host = TestHelpers.GetJobHost(loggerFactory, nameof(ActivityWithRetry_NullRetryOptions)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(40), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains("Value cannot be null.\r\nParameter name: retryOptions"));

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent orchestrator function.
        /// </summary>
        [Fact]
        public async Task StartOrchestration_OnUnregisteredOrchestrator()
        {
            const string activityFunctionName = "UnregisteredOrchestrator";
            string errorMessage = $"The function '{activityFunctionName}' doesn't exist, is disabled, or is not an orchestrator function";

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(StartOrchestration_OnUnregisteredOrchestrator)))
            {
                await host.StartAsync();

                Exception ex = await Assert.ThrowsAsync<FunctionInvocationException>(async () => await host.StartFunctionAsync("UnregisteredOrchestrator", "Unregistered", this.output));
                
                Assert.NotNull(ex.InnerException);
                Assert.Contains(errorMessage, ex.InnerException?.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent activity function.
        /// </summary>
        [Fact]
        public async Task Orchestration_OnUnregisteredActivity()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallActivity)
            };
            const string activityFunctionName = "UnregisteredActivity";
            string errorMessage = $"The function '{activityFunctionName}' doesn't exist, is disabled, or is not an activity function";

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(Orchestration_OnUnregisteredActivity)))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = activityFunctionName,
                    Input = new {Foo = "Bar"}
                };

                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains(errorMessage));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "Orchestration_OnUnregisteredActivity",
                    orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls another orchestrator function.
        /// </summary>
        [Fact]
        public async Task Orchestration_OnValidOrchestrator()
        {
            const string greetingName = "ValidOrchestrator";
            const string validOrchestratorName = "SayHelloWithActivity";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                validOrchestratorName
            };
            string activityFunctionName = nameof(TestActivities.Hello);

            var input = new { Foo = greetingName };
            var inputJson = JsonConvert.SerializeObject(input);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(Orchestration_OnValidOrchestrator)))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = orchestratorFunctionNames[1],
                    Input = input
                };

                // Function type call chain: 'CallActivity' (orchestrator) -> 'SayHelloWithActivity' (orchestrator) -> 'Hello' (activity)
                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);
                var statusInput = JsonConvert.DeserializeObject<Dictionary<string, object>>(status?.Input.ToString());

                Assert.NotNull(status);
                Assert.Equal("Completed", status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(validOrchestratorName, statusInput["FunctionName"].ToString());
                Assert.Contains(greetingName, statusInput["Input"].ToString());
                Assert.Equal($"Hello, [{inputJson}]!", status.Output.ToString());

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(loggerProvider, "Orchestration_OnValidOrchestrator",
                        orchestratorFunctionNames, activityFunctionName);
                }
            }
        }

        [Fact]
        public async Task ThrowExceptionOnLongTimer()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(Orchestration_OnValidOrchestrator)))
            {
                await host.StartAsync();

                // Right now, the limit for timers is 6 days. In the future, we'll extend this and update this test.
                // https://github.com/Azure/azure-functions-durable-extension/issues/14
                DateTime fireAt = DateTime.UtcNow.AddDays(7);
                var client = await host.StartFunctionAsync(nameof(TestOrchestrations.Timer), fireAt, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.NotNull(status);
                Assert.Equal("Failed", status.RuntimeStatus);
                Assert.True(status.Output.ToString().Contains("fireAt"));

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent activity function.
        /// </summary>
        [Fact]
        public async Task Orchestration_OnUnregisteredOrchestrator()
        {
            const string unregisteredOrchestrator = "UnregisteredOrchestrator";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                unregisteredOrchestrator
            };
            
            string errorMessage = $"The function '{unregisteredOrchestrator}' doesn't exist, is disabled, or is not an orchestrator function";

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(Orchestration_OnUnregisteredActivity)))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = unregisteredOrchestrator,
                    Input = new { Foo = "Bar" }
                };

                var client = await host.StartFunctionAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal("Failed", status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-webjobs-sdk-script-pr/issues/36
                Assert.True(status?.Output.ToString().Contains(errorMessage));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(loggerProvider, "Orchestration_OnUnregisteredOrchestrator",
                    orchestratorFunctionNames);
            }
        }
    }
}

