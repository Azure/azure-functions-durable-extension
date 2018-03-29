// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
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

        private static readonly string InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

        public DurableTaskEndToEndTests(ITestOutputHelper output)
        {
            this.output = output;

            // Set to false to manually verify log entries in Application Insights but tests with TestHelpers.AssertLogMessageSequence will be skipped
            this.useTestLogger = true;

            this.loggerProvider = new TestLoggerProvider();
            this.loggerFactory = new LoggerFactory();

            if (this.useTestLogger)
            {
                this.loggerFactory.AddProvider(this.loggerProvider);
            }
            else
            {
                if (!string.IsNullOrEmpty(InstrumentationKey))
                {
                    var filter = new LogCategoryFilter
                    {
                        DefaultLevel = LogLevel.Debug,
                    };

                    filter.CategoryLevels[TestHelpers.LogCategory] = LogLevel.Debug;

                    this.loggerFactory = new LoggerFactory()
                        .AddApplicationInsights(InstrumentationKey, filter.Filter);
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
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.HelloWorldOrchestration_Inline)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(this.loggerProvider, "HelloWorldOrchestration_Inline", orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which runs a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Fact]
        public async Task HelloWorldOrchestration_Activity()
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic();
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information is provided.
        /// </summary>
        [Fact]
        public async Task HelloWorldOrchestration_Activity_History()
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(true);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information with input and result date is provided.
        /// </summary>
        [Fact]
        public async Task HelloWorldOrchestration_Activity_HistoryInputOutput()
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(true, true);
        }

        private async Task HelloWorldOrchestration_Activity_Main_Logic(bool showHistory = false, bool showHistoryOutput = false)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.HelloWorldOrchestration_Activity)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output, showHistory, showHistoryOutput);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);
                if (!showHistory)
                {
                    Assert.Equal(null, status.History);
                }
                else
                {
                    Assert.Equal(3, status.History.Count);
                    Assert.Equal<string>("ExecutionStarted", status.History[0]["EventType"].ToString());
                    Assert.Equal<string>("SayHelloWithActivity", status.History[0]["FunctionName"].ToString());
                    Assert.Equal<string>("TaskCompleted", status.History[1]["EventType"].ToString());
                    Assert.Equal<string>("Hello", status.History[1]["FunctionName"].ToString());
                    if (DateTime.TryParse(status.History[1]["Timestamp"].ToString(), out DateTime timestamp) &&
                        DateTime.TryParse(status.History[1]["ScheduledTime"].ToString(), out DateTime scheduledTime))
                    {
                        Assert.True(timestamp >= scheduledTime);
                    }

                    Assert.Equal<string>("ExecutionCompleted", status.History[2]["EventType"].ToString());
                    Assert.Equal<string>("Completed", status.History[2]["OrchestrationStatus"].ToString());

                    if (showHistoryOutput)
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.NotNull(status.History[1]["Result"]);
                        Assert.Equal<string>("Hello, World!", status.History[1]["Result"].ToString());
                        Assert.NotNull(status.History[2]["Result"]);
                        Assert.Equal<string>("Hello, World!", status.History[2]["Result"].ToString());
                    }
                    else
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.Null(status.History[1]["Result"]);
                        Assert.Null(status.History[2]["Result"]);
                    }

                    Assert.NotNull(status.History);
                }

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(
                    this.loggerProvider,
                    "HelloWorldOrchestration_Activity",
                    orchestratorFunctionNames,
                    activityFunctionName);
            }
        }

        /// <summary>
        /// End-to-end test which validates function chaining by implementing a naive factorial function orchestration.
        /// </summary>
        [Fact]
        public async Task SequentialOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.SequentialOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Factorial), 10, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(10, status?.Input);
                Assert.Equal(3628800, status?.Output);

                await host.StopAsync();
            }

            // Assert log entry count
            if (this.useTestLogger)
            {
                var logger = this.loggerProvider.CreatedLoggers.Single(l => l.Category == TestHelpers.LogCategory);
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
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.ParallelOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.DiskUsage), Environment.CurrentDirectory, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(90), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
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
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.ActorOrchestration)))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                // Perform some operations
                await client.RaiseEventAsync("operation", "incr");

                // TODO: Sleeping to avoid a race condition where multiple ContinueAsNew messages
                //       are processed by the same instance at the same time, resulting in a corrupt
                //       storage failure in DTFx.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/67
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
                Assert.True(
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                // The end message will cause the actor to complete itself.
                await client.RaiseEventAsync("operation", "end");

                status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(3, (int)status?.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.NotEqual(initialValue, status?.Input);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                var logger = this.loggerProvider.CreatedLoggers.Single(l => l.Category == TestHelpers.LogCategory);
                var logMessages = logger.LogMessages.ToList();
                Assert.Equal(49, logMessages.Count);
            }
        }

        /// <summary>
        /// End-to-end test which validates the wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Fact]
        public async Task BatchedActorOrchestration()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.BatchedActorOrchestration)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.BatchActor), null, this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                // Perform some operations
                await client.RaiseEventAsync("newItem", "item1");
                await client.RaiseEventAsync("newItem", "item2");
                await client.RaiseEventAsync("newItem", "item3");
                await client.RaiseEventAsync("newItem", "item4");

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.GetStatusAsync();
                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Sending this last item will cause the actor to complete itself.
                await client.RaiseEventAsync("newItem", "item5");

                status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                await host.StopAsync();
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
                nameof(TestOrchestrations.Counter),
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.TerminateOrchestration)))
            {
                await host.StartAsync();

                // Using the counter orchestration because it will wait indefinitely for input.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], 0, this.output);

                // Need to wait for the instance to start before we can terminate it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                await client.TerminateAsync("sayōnara");

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Terminated, status?.RuntimeStatus);
                Assert.Equal("sayōnara", status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(this.loggerProvider, "TerminateOrchestration", orchestratorFunctionNames);
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
                nameof(TestOrchestrations.Approval),
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.TimerCancellation)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);
                await client.RaiseEventAsync("approval", eventData: true);

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Approved", status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(this.loggerProvider, "TimerCancellation", orchestratorFunctionNames);
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
                nameof(TestOrchestrations.Approval),
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.TimerExpiration)))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], timeout, this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                // Don't send any notification - let the internal timeout expire

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(20), this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Expired", status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(this.loggerProvider, "TimerExpiration", orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which validates that orchestrations run concurrently of each other (up to 100 by default).
        /// </summary>
        [Fact]
        public async Task OrchestrationConcurrency()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.OrchestrationConcurrency)))
            {
                await host.StartAsync();

                Func<Task> orchestrationStarter = async () =>
                {
                    var timeout = TimeSpan.FromSeconds(10);
                    var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Approval), timeout, this.output);
                    await client.WaitForCompletionAsync(TimeSpan.FromSeconds(60), this.output);

                    // Don't send any notification - let the internal timeout expire
                };

                int iterations = 100;
                var tasks = new Task[iterations];
                for (int i = 0; i < iterations; i++)
                {
                    tasks[i] = orchestrationStarter();
                }

                // The 100 orchestrations above (which each delay for 10 seconds) should all complete in less than 70 seconds.
                Task parallelOrchestrations = Task.WhenAll(tasks);
                Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(70));

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
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.HandledActivityException)))
            {
                await host.StartAsync();

                // Empty string input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.TryCatchLoop), 5, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
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
                nameof(TestOrchestrations.Throw),
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.UnhandledOrchestrationException)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains("Value cannot be null"));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(this.loggerProvider, "UnhandledOrchestrationException", orchestratorFunctionNames);
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
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.Orchestration_Activity)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(20), this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(orchestratorFunctionNames[0], status?.Name);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(
                    this.loggerProvider,
                    "Orchestration_Activity",
                    orchestratorFunctionNames,
                    activityFunctionName);
            }
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs.
        /// </summary>
        [Fact]
        public async Task SubOrchestration_ComplexType()
        {
            await this.SubOrchestration_ComplexType_Main_Logic();
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information is provided.
        /// </summary>
        [Fact]
        public async Task SubOrchestration_ComplexType_History()
        {
            await this.SubOrchestration_ComplexType_Main_Logic(true);
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information with input and result data is provided.
        /// </summary>
        [Fact]
        public async Task SubOrchestration_ComplexType_HistoryInputOutput()
        {
            await this.SubOrchestration_ComplexType_Main_Logic(true, true);
        }

        private async Task SubOrchestration_ComplexType_Main_Logic(bool showHistory = false, bool showHistoryOutput = false)
        {
            const string TaskHub = nameof(this.SubOrchestration_ComplexType);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, TaskHub))
            {
                await host.StartAsync();

                var complexTypeDataInput = new ComplexType
                {
                    A = -42,
                    B = new List<DateTime> { DateTime.UtcNow, DateTime.UtcNow.AddYears(1) },
                    C = ComplexType.CustomEnum.Value2,
                    D = new ComplexType.ComplexInnerType
                    {
                        E = Guid.NewGuid().ToString(),
                        F = TimeSpan.FromHours(1.5),
                    },
                };

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.CallActivity),
                    Input = new StartOrchestrationArgs
                    {
                        FunctionName = nameof(TestActivities.Echo),
                        Input = complexTypeDataInput,
                    },
                };

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(
                    Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(20),
                    this.output,
                    showHistory,
                    showHistoryOutput);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);

                Assert.NotNull(status.Output);
                ComplexType complextTypeDataOutput = status.Output.ToObject<ComplexType>();
                CompareTwoComplexTypeObjects(complexTypeDataInput, complextTypeDataOutput);

                if (!showHistory)
                {
                    Assert.Equal(null, status.History);
                }
                else
                {
                    Assert.Equal(3, status.History.Count);
                    Assert.Equal<string>("ExecutionStarted", status.History[0]["EventType"].ToString());
                    Assert.Equal<string>("CallOrchestrator", status.History[0]["FunctionName"].ToString());
                    Assert.Equal<string>("SubOrchestrationInstanceCompleted", status.History[1]["EventType"].ToString());
                    Assert.Equal<string>("CallActivity", status.History[1]["FunctionName"].ToString());
                    if (DateTime.TryParse(status.History[1]["Timestamp"].ToString(), out DateTime timestamp) &&
                        DateTime.TryParse(status.History[1]["FunctionName"].ToString(), out DateTime scheduledTime))
                    {
                        Assert.True(timestamp > scheduledTime);
                    }

                    Assert.Equal<string>("ExecutionCompleted", status.History[2]["EventType"].ToString());
                    Assert.Equal<string>("Completed", status.History[2]["OrchestrationStatus"].ToString());

                    if (showHistoryOutput)
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.Null(status.History[1]["Input"]);
                        Assert.NotNull(status.History[1]["Result"]);
                        var resultSubOrchestrationInstanceCompleted = JsonConvert.DeserializeObject<ComplexType>(status.History[1]["Result"].ToString());
                        CompareTwoComplexTypeObjects(complexTypeDataInput, resultSubOrchestrationInstanceCompleted);
                        Assert.NotNull(status.History[2]["Result"]);
                        var resultExecutionCompleted = JsonConvert.DeserializeObject<ComplexType>(status.History[2]["Result"].ToString());
                        CompareTwoComplexTypeObjects(complexTypeDataInput, resultExecutionCompleted);
                    }
                    else
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.Null(status.History[1]["Result"]);
                        Assert.Null(status.History[2]["Result"]);
                    }

                    Assert.NotNull(status.History);
                }

                await host.StopAsync();
            }
        }

        private static void CompareTwoComplexTypeObjects(ComplexType firstObject, ComplexType secondObject)
        {
            Assert.NotNull(secondObject);
            Assert.Equal(firstObject.A, secondObject.A);
            Assert.Equal(firstObject.B[0], secondObject.B[0]);
            Assert.Equal(firstObject.B[1], secondObject.B[1]);
            Assert.NotNull(secondObject.D);
            Assert.Equal(firstObject.D.E, secondObject.D.E);
            Assert.Equal(firstObject.D.F, secondObject.D.F);
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from orchestrator functions.
        /// </summary>
        [Fact]
        public async Task UnhandledOrchestrationExceptionWithRetry()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorThrowWithRetry),
            };

            using (JobHost host =
                TestHelpers.GetJobHost(this.loggerFactory, nameof(this.UnhandledOrchestrationExceptionWithRetry)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(50), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains("Value cannot be null"));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(
                    this.loggerProvider,
                    "UnhandledOrchestrationExceptionWithRetry",
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
                nameof(TestOrchestrations.OrchestratorWithRetry_NullRetryOptions),
            };

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.OrchestrationWithRetry_NullRetryOptions)))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(50), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains("Value cannot be null."));
                Assert.True(status?.Output.ToString().Contains("Parameter name: retryOptions"));

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
                nameof(TestOrchestrations.Throw),
            };

            string activityFunctionName = nameof(TestActivities.Throw);

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.UnhandledActivityException)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(60);

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-functions-durable-extension/issues/84
                Assert.StartsWith($"The activity function '{activityFunctionName}' failed.", (string)status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(
                    this.loggerProvider,
                    "UnhandledActivityException",
                    orchestratorFunctionNames,
                    activityFunctionName);
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
                nameof(TestOrchestrations.ActivityThrowWithRetry),
            };

            string activityFunctionName = nameof(TestActivities.Throw);

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.UnhandledActivityExceptionWithRetry)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(40), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-functions-durable-extension/issues/84
                Assert.StartsWith($"The activity function '{activityFunctionName}' failed.", (string)status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(
                    this.loggerProvider,
                    "UnhandledActivityExceptionWithRetry",
                    orchestratorFunctionNames,
                    activityFunctionName);
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an activity function with null RetryOptions fails.
        /// </summary>
        [Fact]
        public async Task ActivityWithRetry_NullRetryOptions()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.ActivityWithRetry_NullRetryOptions)))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                string orchestratorFunctionName = nameof(TestOrchestrations.ActivityWithRetry_NullRetryOptions);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionName, message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(40), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains("Value cannot be null."));
                Assert.True(status?.Output.ToString().Contains("Parameter name: retryOptions"));

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

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.StartOrchestration_OnUnregisteredOrchestrator)))
            {
                await host.StartAsync();

                Exception ex = await Assert.ThrowsAsync<FunctionInvocationException>(async () => await host.StartOrchestratorAsync("UnregisteredOrchestrator", "Unregistered", this.output));

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
                nameof(TestOrchestrations.CallActivity),
            };

            const string activityFunctionName = "UnregisteredActivity";
            string errorMessage = $"The function '{activityFunctionName}' doesn't exist, is disabled, or is not an activity function";

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.Orchestration_OnUnregisteredActivity)))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = activityFunctionName,
                    Input = new { Foo = "Bar" },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                // There aren't any exception details in the output: https://github.com/Azure/azure-functions-durable-extension/issues/84
                Assert.StartsWith(errorMessage, (string)status?.Output);

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(
                    this.loggerProvider,
                    "Orchestration_OnUnregisteredActivity",
                    orchestratorFunctionNames);
            }
        }

        /// <summary>
        /// End-to-end test which runs an orchestrator function that calls another orchestrator function.
        /// </summary>
        [Fact]
        public async Task Orchestration_OnValidOrchestrator()
        {
            const string greetingName = "ValidOrchestrator";
            const string validOrchestratorName = "SayHelloWithActivity";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                validOrchestratorName,
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            var input = new { Foo = greetingName };
            var inputJson = JsonConvert.SerializeObject(input);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.Orchestration_OnValidOrchestrator)))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = orchestratorFunctionNames[1],
                    Input = inputJson,
                };

                // Function type call chain: 'CallActivity' (orchestrator) -> 'SayHelloWithActivity' (orchestrator) -> 'Hello' (activity)
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);
                var statusInput = JsonConvert.DeserializeObject<Dictionary<string, object>>(status?.Input.ToString());

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(validOrchestratorName, statusInput["FunctionName"].ToString());
                Assert.Contains(greetingName, statusInput["Input"].ToString());
                Assert.Equal($"Hello, {inputJson}!", status.Output.ToString());

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.loggerProvider,
                        "Orchestration_OnValidOrchestrator",
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        [Fact]
        public async Task ThrowExceptionOnLongTimer()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.Orchestration_OnValidOrchestrator)))
            {
                await host.StartAsync();

                // Right now, the limit for timers is 6 days. In the future, we'll extend this and update this test.
                // https://github.com/Azure/azure-functions-durable-extension/issues/14
                DateTime fireAt = DateTime.UtcNow.AddDays(7);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Timer), fireAt, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
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
                unregisteredOrchestrator,
            };

            string errorMessage = $"The function '{unregisteredOrchestrator}' doesn't exist, is disabled, or is not an orchestrator function";

            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, nameof(this.Orchestration_OnUnregisteredActivity)))
            {
                await host.StartAsync();

                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = unregisteredOrchestrator,
                    Input = new { Foo = "Bar" },
                };

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains(errorMessage));

                await host.StopAsync();
            }

            if (this.useTestLogger)
            {
                TestHelpers.AssertLogMessageSequence(
                    this.loggerProvider,
                    "Orchestration_OnUnregisteredOrchestrator",
                    orchestratorFunctionNames);
            }
        }

        [Fact]
        public async Task BigReturnValue_Orchestrator()
        {
            string taskHub = nameof(this.BigReturnValue_Orchestrator);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, taskHub))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.BigReturnValue);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;

                var client = await host.StartOrchestratorAsync(orchestrator, stringLength, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);
                Assert.True(status?.Output.ToString().Contains("The UTF-16 size of the JSON-serialized payload must not exceed 60 KB"));

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task BigReturnValue_Activity()
        {
            string taskHub = nameof(this.BigReturnValue_Activity);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, taskHub))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.CallActivity);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;
                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestActivities.BigReturnValue),
                    Input = stringLength,
                };

                var client = await host.StartOrchestratorAsync(orchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                // Activity function exception details are not captured in the orchestrator output:
                // https://github.com/Azure/azure-functions-durable-extension/issues/84
                ////Assert.True(status?.Output.ToString().Contains("The UTF-16 size of the JSON-serialized payload must not exceed 60 KB"));
                Assert.StartsWith($"The activity function '{input.FunctionName}' failed.", (string)status?.Output);

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task RaiseEventToSubOrchestration()
        {
            string taskHub = nameof(this.RaiseEventToSubOrchestration);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerFactory, taskHub))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.CallOrchestrator);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.Approval),
                    InstanceId = "SubOrchestration-" + Guid.NewGuid().ToString("N"),
                    Input = TimeSpan.FromMinutes(5),
                };

                var client = await host.StartOrchestratorAsync(orchestrator, input, this.output);
                var status = await client.WaitForStartupAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Wait long enough for the sub-orchestration to be started and waiting for input.
                await Task.Delay(TimeSpan.FromSeconds(2));
                await client.InnerClient.RaiseEventAsync(input.InstanceId, "approval", true);

                status = await client.WaitForCompletionAsync(timeout, this.output);
                Assert.Equal("Approved", status?.Output);

                await host.StopAsync();
            }
        }

        [DataContract]
        private class ComplexType
        {
            [DataContract]
            public enum CustomEnum
            {
                [EnumMember]
                Value1,

                [EnumMember]
                Value2,
            }

            [DataMember]
            public int A { get; set; }

            [DataMember]
            public List<DateTime> B { get; set; }

            [DataMember]
            public CustomEnum C { get; set; }

            [DataMember]
            public ComplexInnerType D { get; set; }

            [DataContract]
            public class ComplexInnerType
            {
                [DataMember]
                public string E { get; set; }

                [DataMember]
                public TimeSpan F { get; set; }
            }
        }
    }
}
