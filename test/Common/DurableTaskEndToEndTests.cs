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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class DurableTaskEndToEndTests
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;
        private readonly bool useTestLogger = true;

        private static readonly string InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

        public DurableTaskEndToEndTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        /// <summary>
        /// End-to-end test which validates a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HelloWorldOrchestration_Inline(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Inline),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Inline",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates a simple orchestrator function does not have assigned value for <see cref="DurableOrchestrationContext.ParentInstanceId"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ParentInstnaceId_Not_Assigned_In_Orchestrator(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ProvideParentInstanceId),
            };

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Inline),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("", status.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HelloWorldOrchestration_Activity(bool extendedSessions)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(extendedSessions);
        }

        /// <summary>
        /// End-to-end test which validates logs for replay events by a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HelloWorldOrchestration_Activity_Validate_Logs_For_Replay_Events(bool logReplayEvents)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(false, logReplayEvents: logReplayEvents);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HelloWorldOrchestration_Activity_History(bool extendedSessions)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(extendedSessions, true);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information with input and result date is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HelloWorldOrchestration_Activity_HistoryInputOutput(bool extendedSessions)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(extendedSessions, true, true);
        }

        private async Task HelloWorldOrchestration_Activity_Main_Logic(bool extendedSessions, bool showHistory = false, bool showHistoryOutput = false, bool logReplayEvents = true)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Activity),
                extendedSessions,
                logReplayEvents: logReplayEvents))
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
                    Assert.Null(status.History);
                }
                else
                {
                    Assert.Equal(3, status.History.Count);
                    Assert.Equal("ExecutionStarted", status.History[0]["EventType"].ToString());
                    Assert.Equal("SayHelloWithActivity", status.History[0]["FunctionName"].ToString());
                    Assert.Equal("TaskCompleted", status.History[1]["EventType"].ToString());
                    Assert.Equal("Hello", status.History[1]["FunctionName"].ToString());
                    if (DateTime.TryParse(status.History[1]["Timestamp"].ToString(), out DateTime timestamp) &&
                        DateTime.TryParse(status.History[1]["ScheduledTime"].ToString(), out DateTime scheduledTime))
                    {
                        Assert.True(timestamp >= scheduledTime);
                    }

                    Assert.Equal("ExecutionCompleted", status.History[2]["EventType"].ToString());
                    Assert.Equal("Completed", status.History[2]["OrchestrationStatus"].ToString());

                    if (showHistoryOutput)
                    {
                        Assert.Null(status.History[0]["Input"]);
                        Assert.NotNull(status.History[1]["Result"]);
                        Assert.Equal("Hello, World!", status.History[1]["Result"].ToString());
                        Assert.NotNull(status.History[2]["Result"]);
                        Assert.Equal("Hello, World!", status.History[2]["Result"].ToString());
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

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Activity",
                        client.InstanceId,
                        extendedSessions || !logReplayEvents,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HelloWorldOrchestration_Activity_CustomStatus(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityAndCustomStatus),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Activity_CustomStatus),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal(
                    new JObject
                    {
                        { "nextActions", new JArray("A", "B", "C") },
                        { "foo", 2 },
                    },
                    (JToken)status.CustomStatus);
                Assert.Equal("Hello, World!", status.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Activity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates function chaining by implementing a naive factorial function orchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SequentialOrchestration(bool extendedSessions)
        {
            string instanceId;
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.SequentialOrchestration),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Factorial), 10, this.output);
                instanceId = client.InstanceId;

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
                var logMessages = logger.LogMessages.Where(
                    msg => msg.FormattedMessage.Contains(instanceId)).ToList();

                int expectedLogMessageCount = extendedSessions ? 43 : 153;
                Assert.Equal(expectedLogMessageCount, logMessages.Count);
            }
        }

        /// <summary>
        /// End-to-end test which validates parallel function execution by enumerating all files in the current directory
        /// in parallel and getting the sum total of all file sizes.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ParallelOrchestration(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ParallelOrchestration),
                extendedSessions))
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
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ActorOrchestration(bool extendedSessions)
        {
            string instanceId;
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActorOrchestration),
                extendedSessions))
            {
                await host.StartAsync();

                int initialValue = 0;
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);
                instanceId = client.InstanceId;

                // Wait for the instance to go into the Running state. This is necessary to ensure log validation consistency.
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
        /// End-to-end test which validates the wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BatchedActorOrchestration(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.BatchedActorOrchestration),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.BatchActor), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("newItem", "item1");
                await client.RaiseEventAsync("newItem", "item2");
                await client.RaiseEventAsync("newItem", "item3");
                await client.RaiseEventAsync("newItem", "item4");

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Sending this last item will cause the actor to complete itself.
                await client.RaiseEventAsync("newItem", "item5");

                status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the parallel wait-for-full-batch case using an actor pattern.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ParallelBatchedActorOrchestration(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ParallelBatchedActorOrchestration),
                extendedSessions))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ParallelBatchActor), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("newItem", "item1");
                await client.RaiseEventAsync("newItem", "item2");
                await client.RaiseEventAsync("newItem", "item3");

                // Make sure it's still running and didn't complete early (or fail).
                await Task.Delay(TimeSpan.FromSeconds(2));
                var status = await client.GetStatusAsync();
                Assert.Equal(OrchestrationRuntimeStatus.Running, status?.RuntimeStatus);

                // Sending this last item will cause the actor to complete itself.
                await client.RaiseEventAsync("newItem", "item4");
                status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExternalEvents_MultipleNamesLooping(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ExternalEvents_MultipleNamesLooping),
                extendedSessions))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter2), null, this.output);

                // Perform some operations
                await client.RaiseEventAsync("incr", null);
                await client.RaiseEventAsync("incr", null);
                await client.RaiseEventAsync("done", null);

                // Make sure it actually completed
                var status = await client.WaitForCompletionAsync(
                    TimeSpan.FromSeconds(1000),
                    this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(2, status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the Terminate functionality.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TerminateOrchestration(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Counter),
            };

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TerminateOrchestration),
                extendedSessions))
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
        /// End-to-end test which validates the Rewind functionality.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        public async Task RewindOrchestration()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityForRewind),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.RewindOrchestration),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "Catherine", this.output);

                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                var statusFail = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, statusFail?.RuntimeStatus);

                TestOrchestrations.SayHelloWithActivityForRewindShouldFail = false;

                await client.RewindAsync("rewind!");

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Hello, Catherine!", status?.Output);

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
        [InlineData(true)]
        [InlineData(false)]
        public async Task TimerCancellation(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval),
            };

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TimerCancellation),
                extendedSessions))
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
        [InlineData(true)]
        [InlineData(false)]
        public async Task TimerExpiration(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.Approval),
            };

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TimerExpiration),
                extendedSessions))
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
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TimerExpiration),
                extendedSessions))
            {
                await host.StartAsync();

                var timeout = TimeSpan.FromSeconds(10);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], (timeout, defaultValue), this.output);

                // Need to wait for the instance to start before sending events to it.
                // TODO: This requirement may not be ideal and should be revisited.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/101
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                // Don't send any notification - let the internal timeout expire
                if (sendEvent)
                {
                    await client.RaiseEventAsync("Approval", "ApprovalValue");
                }

                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(20), this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(expectedResponse, status?.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that orchestrations run concurrently of each other (up to 100 by default).
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OrchestrationConcurrency(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationConcurrency),
                extendedSessions))
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

                // The 100 orchestrations above (which each delay for 10 seconds) should all complete in less than 1000 seconds (~16 minutes).
                Task parallelOrchestrations = Task.WhenAll(tasks);
                Task timeoutTask = Task.Delay(TimeSpan.FromMinutes(3));

                Task winner = await Task.WhenAny(parallelOrchestrations, timeoutTask);
                Assert.Equal(parallelOrchestrations, winner);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the orchestrator's exception handling behavior.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HandledActivityException(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HandledActivityException),
                extendedSessions))
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
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnhandledOrchestrationException(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ThrowOrchestrator),
            };

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledOrchestrationException),
                extendedSessions))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = status.Output.ToString();
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith($"Orchestrator function '{orchestratorFunctionNames[0]}' failed: Value cannot be null.", output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledOrchestrationException",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates calling an orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Orchestration_Activity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorGreeting),
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_Activity),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(20), this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);
                Assert.Equal(orchestratorFunctionNames[0], status?.Name);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_Activity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task SubOrchestration_ComplexType()
        {
            await this.SubOrchestration_ComplexType_Main_Logic();
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information is provided.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task SubOrchestration_ComplexType_History()
        {
            await this.SubOrchestration_ComplexType_Main_Logic(true);
        }

        /// <summary>
        /// End-to-end test which ensures sub-orchestrations can work with complex types for inputs and outputs and history information with input and result data is provided.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task SubOrchestration_ComplexType_HistoryInputOutput()
        {
            await this.SubOrchestration_ComplexType_Main_Logic(true, true);
        }

        /// <summary>
        /// End-to-end test which validates a sub-orchestrator function have assigned corrent value for <see cref="DurableOrchestrationContext.ParentInstanceId"/>.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SubOrchestration_Has_Valid_ParentInstanceId_Assigned(bool extendedSessions)
        {
            const string TaskHub = nameof(this.SubOrchestration_ComplexType);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, TaskHub, extendedSessions))
            {
                await host.StartAsync();

                string parentOrchestrator = nameof(TestOrchestrations.CallOrchestrator);

                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestOrchestrations.ProvideParentInstanceId),
                };

                var client = await host.StartOrchestratorAsync(parentOrchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(
                    Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(20),
                    this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(client.InstanceId, status.InstanceId);

                Assert.NotNull(status.Output);
                Assert.Equal(status.Output, client.InstanceId);

                await host.StopAsync();
            }
        }

        private async Task SubOrchestration_ComplexType_Main_Logic(bool showHistory = false, bool showHistoryOutput = false)
        {
            const string TaskHub = nameof(this.SubOrchestration_ComplexType);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, TaskHub, enableExtendedSessions: false))
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
                    Assert.Null(status.History);
                }
                else
                {
                    Assert.Equal(3, status.History.Count);
                    Assert.Equal("ExecutionStarted", status.History[0]["EventType"].ToString());
                    Assert.Equal("CallOrchestrator", status.History[0]["FunctionName"].ToString());
                    Assert.Equal("SubOrchestrationInstanceCompleted", status.History[1]["EventType"].ToString());
                    Assert.Equal("CallActivity", status.History[1]["FunctionName"].ToString());
                    if (DateTime.TryParse(status.History[1]["Timestamp"].ToString(), out DateTime timestamp) &&
                        DateTime.TryParse(status.History[1]["FunctionName"].ToString(), out DateTime scheduledTime))
                    {
                        Assert.True(timestamp > scheduledTime);
                    }

                    Assert.Equal("ExecutionCompleted", status.History[2]["EventType"].ToString());
                    Assert.Equal("Completed", status.History[2]["OrchestrationStatus"].ToString());

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
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnhandledOrchestrationExceptionWithRetry(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorThrowWithRetry),
            };

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledOrchestrationExceptionWithRetry),
                extendedSessions))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(50), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = status.Output.ToString();
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith($"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The orchestrator function 'ThrowOrchestrator' failed: \"Value cannot be null.\"", output);

                string subOrchestrationInstanceId = (string)status.CustomStatus;
                Assert.NotNull(subOrchestrationInstanceId);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.UnhandledOrchesterationExceptionWithRetry_AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledOrchestrationExceptionWithRetry",
                        subOrchestrationInstanceId,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an orchestrator function with null RetryOptions fails.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OrchestrationWithRetry_NullRetryOptions()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestratorWithRetry_NullRetryOptions),
            };

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationWithRetry_NullRetryOptions),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                // Null input should result in ArgumentNullException in the orchestration code.
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(50), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = status.Output.ToString().Replace(Environment.NewLine, " ");
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: Value cannot be null. Parameter name: retryOptions",
                    output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates the handling of unhandled exceptions generated from activity code.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnhandledActivityException(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ThrowOrchestrator),
            };

            string activityFunctionName = nameof(TestActivities.ThrowActivity);

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledActivityException),
                extendedSessions))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(60);

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = (string)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The activity function '{activityFunctionName}' failed: \"{message}\"",
                    output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledActivityException",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries of unhandled exceptions generated from activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnhandledActivityExceptionWithRetry(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ActivityThrowWithRetry),
            };

            string activityFunctionName = nameof(TestActivities.ThrowActivity);

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.UnhandledActivityExceptionWithRetry),
                extendedSessions))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(40), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = (string)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.StartsWith(
                    $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The activity function '{activityFunctionName}' failed: \"{message}\"",
                    output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "UnhandledActivityExceptionWithRetry",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        /// <summary>
        /// End-to-end test which validates the retries for an activity function with null RetryOptions fails.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ActivityWithRetry_NullRetryOptions()
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ActivityWithRetry_NullRetryOptions),
                enableExtendedSessions: false))
            {
                await host.StartAsync();

                string message = "Kah-BOOOOM!!!";
                string orchestratorFunctionName = nameof(TestOrchestrations.ActivityWithRetry_NullRetryOptions);
                var client = await host.StartOrchestratorAsync(orchestratorFunctionName, message, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(40), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status?.RuntimeStatus);

                string output = (string)status?.Output;
                this.output.WriteLine($"Orchestration output string: {output}");
                Assert.Contains(orchestratorFunctionName, output);
                Assert.Contains("Value cannot be null.", output);
                Assert.Contains("Parameter name: retryOptions", output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task StartOrchestration_OnUnregisteredOrchestrator(bool extendedSessions)
        {
            const string activityFunctionName = "UnregisteredOrchestrator";
            string errorMessage = $"The function '{activityFunctionName}' doesn't exist, is disabled, or is not an orchestrator function";

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.StartOrchestration_OnUnregisteredOrchestrator),
                extendedSessions))
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
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Orchestration_OnUnregisteredActivity(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallActivity),
            };

            const string activityFunctionName = "UnregisteredActivity";
            string errorMessage = $"Orchestrator function '{orchestratorFunctionNames[0]}' failed: The function '{activityFunctionName}' doesn't exist, is disabled, or is not an activity function";

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnUnregisteredActivity),
                extendedSessions))
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
                Assert.StartsWith(errorMessage, (string)status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnUnregisteredActivity",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        /// <summary>
        /// End-to-end test which runs an orchestrator function that calls another orchestrator function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Orchestration_OnValidOrchestrator(bool extendedSessions)
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
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnValidOrchestrator),
                extendedSessions))
            {
                await host.StartAsync();

                string parentInstanceId = "PARENT_" + Guid.NewGuid().ToString("N");
                var startArgs = new StartOrchestrationArgs
                {
                    FunctionName = orchestratorFunctionNames[1],
                    InstanceId = parentInstanceId + ":0",
                    Input = inputJson,
                };

                // Function type call chain: 'CallActivity' (orchestrator) -> 'SayHelloWithActivity' (orchestrator) -> 'Hello' (activity)
                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], startArgs, this.output, parentInstanceId);
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
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnValidOrchestrator",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ThrowExceptionOnLongTimer(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnValidOrchestrator),
                extendedSessions))
            {
                await host.StartAsync();

                // Right now, the limit for timers is 6 days. In the future, we'll extend this and update this test.
                // https://github.com/Azure/azure-functions-durable-extension/issues/14
                DateTime fireAt = DateTime.UtcNow.AddDays(7);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Timer), fireAt, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
                Assert.Contains("fireAt", status.Output.ToString());

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which runs a orchestrator function that calls a non-existent activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Orchestration_OnUnregisteredOrchestrator(bool extendedSessions)
        {
            const string unregisteredOrchestrator = "UnregisteredOrchestrator";
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.CallOrchestrator),
                unregisteredOrchestrator,
            };

            string errorMessage = $"The function '{unregisteredOrchestrator}' doesn't exist, is disabled, or is not an orchestrator function";

            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Orchestration_OnUnregisteredActivity),
                extendedSessions))
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

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "Orchestration_OnUnregisteredOrchestrator",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BigReturnValue_Orchestrator(bool extendedSessions)
        {
            string taskHub = nameof(this.BigReturnValue_Orchestrator);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.BigReturnValue);
                var timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;

                var client = await host.StartOrchestratorAsync(orchestrator, stringLength, this.output);
                var status = await client.WaitForCompletionAsync(timeout, this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(stringLength, status?.Output.ToString().Count(c => c == TestOrchestrations.BigValueChar));

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BigReturnValue_Activity(bool extendedSessions)
        {
            string taskHub = nameof(this.BigReturnValue_Activity);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions))
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

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(stringLength, status?.Output.ToString().Count(c => c == TestActivities.BigValueChar));

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RaiseEventToSubOrchestration(bool extendedSessions)
        {
            string taskHub = nameof(this.RaiseEventToSubOrchestration);
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, taskHub, extendedSessions))
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

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [Trait("Category", PlatformSpecificHelpers.TestCategory + "_BVT")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SetStatusOrchestration(bool extendedSessions)
        {
            using (JobHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.SetStatusOrchestration),
                extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SetStatus), null, this.output);
                await client.WaitForStartupAsync(TimeSpan.FromSeconds(10), this.output);

                DurableOrchestrationStatus orchestrationStatus = await client.GetStatusAsync();
                Assert.Equal(JTokenType.Null, orchestrationStatus.CustomStatus?.Type);

                // The orchestrator will wait for an external event, and use the payload to update its custom status.
                await client.RaiseEventAsync("UpdateStatus", "updated status");
                await Task.Delay(TimeSpan.FromSeconds(2));
                orchestrationStatus = await client.GetStatusAsync();
                Assert.Equal("updated status", (string)orchestrationStatus.CustomStatus);

                // Test clearing an existing custom status
                await client.RaiseEventAsync("UpdateStatus", null);
                await Task.Delay(TimeSpan.FromSeconds(2));
                orchestrationStatus = await client.GetStatusAsync();
                Assert.Equal(JTokenType.Null, orchestrationStatus.CustomStatus?.Type);

                // Test setting the custom status to a complex object.
                var newCustomStatus = new { Foo = "Bar", Count = 2, };
                await client.RaiseEventAsync("UpdateStatus", newCustomStatus);
                orchestrationStatus = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);
                Assert.Equal(newCustomStatus.Foo, (string)orchestrationStatus.CustomStatus["Foo"]);
                Assert.Equal(newCustomStatus.Count, (int)orchestrationStatus.CustomStatus["Count"]);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, orchestrationStatus?.RuntimeStatus);

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatus_InstanceNotFound()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.GetStatus_InstanceNotFound), false))
            {
                await host.StartAsync();

                // Start a dummy orchestration just to help us get a client object
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloInline), null, this.output);
                await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                string bogusInstanceId = "BOGUS_" + Guid.NewGuid().ToString("N");
                this.output.WriteLine($"Fetching status for fake instance: {bogusInstanceId}");
                DurableOrchestrationStatus status = await client.InnerClient.GetStatusAsync(instanceId: bogusInstanceId);
                Assert.Null(status);
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatus_ShowInputFalse()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.GetStatus_ShowInputFalse), false))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 1, this.output);

                DurableOrchestrationStatus status = await client.GetStatusAsync(showHistory: false, showHistoryOutput: false, showInput: false);
                Assert.True(string.IsNullOrEmpty(status.Input.ToString()));
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task GetStatus_ShowInputDefault()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.GetStatus_ShowInputDefault), false))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 1, this.output);

                DurableOrchestrationStatus status = await client.GetStatusAsync(showHistory: false, showHistoryOutput: false);
                Assert.Equal("1", status.Input.ToString());
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task Deserialize_DurableOrchestrationStatus()
        {
            using (JobHost host = TestHelpers.GetJobHost(this.loggerProvider, nameof(this.Deserialize_DurableOrchestrationStatus), false))
            {
                await host.StartAsync();

                string instanceId = Guid.NewGuid().ToString();
                DurableOrchestrationStatus input = new DurableOrchestrationStatus();
                var client = await host.StartOrchestratorAsync(
                    nameof(TestOrchestrations.GetDurableOrchestrationStatus),
                    input,
                    this.output);
                DurableOrchestrationStatus desereliazedStatus = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.NotNull(desereliazedStatus);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, desereliazedStatus.RuntimeStatus);
                Assert.True(desereliazedStatus.LastUpdatedTime > desereliazedStatus.CreatedTime);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates that Activity function can get an instance of HttpManagementPayload and return via the orchestrator.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Activity_Gets_HttpManagementPayload(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.ReturnHttpManagementPayload),
                nameof(TestActivities.GetAndReturnHttpManagementPayload),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Activity_Gets_HttpManagementPayload),
                extendedSessions,
                notificationUrl: new Uri(TestConstants.NotificationUrl)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                HttpManagementPayload httpManagementPayload = status.Output.ToObject<HttpManagementPayload>();
                this.ValidateHttpManagementPayload(httpManagementPayload, extendedSessions, "ActivityGetsHttpManagementPayload");

                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates HttpManagementPayload retrieved from Orchestration client when executing a simple orchestrator function which doesn't call any activity functions.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        public async Task OrchestrationClient_Gets_HttpManagementPayload(bool extendedSessions)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationClient_Gets_HttpManagementPayload),
                extendedSessions,
                notificationUrl: new Uri(TestConstants.NotificationUrl)))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(TimeSpan.FromSeconds(30), this.output);

                HttpManagementPayload httpManagementPayload = client.InnerClient.CreateHttpManagementPayload(status.InstanceId);
                this.ValidateHttpManagementPayload(httpManagementPayload, extendedSessions, "OrchestrationClientGetsHttpManagementPayload");

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();

                if (this.useTestLogger)
                {
                    TestHelpers.AssertLogMessageSequence(
                        this.output,
                        this.loggerProvider,
                        "HelloWorldOrchestration_Inline",
                        client.InstanceId,
                        extendedSessions,
                        orchestratorFunctionNames);
                }
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ActorOrchestration_WithTaskHubName(bool extendedSessions)
        {
            var taskHubName1 = "ActorOrchestration1";
            var taskHubName2 = "ActorOrchestration2";
            using (JobHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions))
            using (JobHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions))
            {
                await host1.StartAsync();
                await host2.StartAsync();

                int initialValue = 0;
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "World", this.output);
                var instanceId = client1.InstanceId;
                taskHubName1 = client1.TaskHubName;
                taskHubName2 = client2.TaskHubName;

                // Perform some operations
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr");

                // TODO: Sleeping to avoid a race condition where multiple ContinueAsNew messages
                //       are processed by the same instance at the same time, resulting in a corrupt
                //       storage failure in DTFx.
                // BUG: https://github.com/Azure/azure-functions-durable-extension/issues/67
                await Task.Delay(3000);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr");
                await Task.Delay(3000);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr");
                await Task.Delay(3000);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "decr");
                await Task.Delay(3000);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "incr");
                await Task.Delay(3000);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client1.GetStatusAsync();
                Assert.True(
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status?.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                // The end message will cause the actor to complete itself.
                await client2.RaiseEventAsync(taskHubName1, instanceId, "operation", "end");

                status = await client1.WaitForCompletionAsync(TimeSpan.FromSeconds(10), this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(3, (int)status?.Output);

                // When using ContinueAsNew, the original input is discarded and replaced with the most recent state.
                Assert.NotEqual(initialValue, status?.Input);

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExternalEvents_WithTaskHubName_MultipleNamesLooping(bool extendedSessions)
        {
            var taskHubName1 = "MultipleNamesLooping1";
            var taskHubName2 = "MultipleNamesLooping2";
            using (JobHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions))
            using (JobHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions))
            {
                await host1.StartAsync();
                await host2.StartAsync();
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.Counter2), null, this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "World", this.output);
                taskHubName1 = client1.TaskHubName;
                taskHubName2 = client2.TaskHubName;
                var instanceId = client1.InstanceId;

                // Perform some operations
                await client2.RaiseEventAsync(taskHubName1, instanceId, "incr", null);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "incr", null);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "done", null);

                // Make sure it actually completed
                var status = await client1.WaitForCompletionAsync(
                    TimeSpan.FromSeconds(1000),
                    this.output);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal(2, status.Output);

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        private void ValidateHttpManagementPayload(HttpManagementPayload httpManagementPayload, bool extendedSessions, string defaultTaskHubName)
        {
            Assert.NotNull(httpManagementPayload);
            Assert.NotEmpty(httpManagementPayload.Id);
            string instanceId = httpManagementPayload.Id;
            string notifucaitonUrl = TestConstants.NotificationUrlBase;
            string taskHubName = extendedSessions
                ? $"{defaultTaskHubName}EX"
                : defaultTaskHubName;
            taskHubName += PlatformSpecificHelpers.VersionSuffix;

            Assert.Equal(
                $"{notifucaitonUrl}/instances/{instanceId}?taskHub={taskHubName}&connection=Storage&code=mykey",
                httpManagementPayload.StatusQueryGetUri);
            Assert.Equal(
                $"{notifucaitonUrl}/instances/{instanceId}/raiseEvent/{{eventName}}?taskHub={taskHubName}&connection=Storage&code=mykey",
                httpManagementPayload.SendEventPostUri);
            Assert.Equal(
                $"{notifucaitonUrl}/instances/{instanceId}/terminate?reason={{text}}&taskHub={taskHubName}&connection=Storage&code=mykey",
                httpManagementPayload.TerminatePostUri);
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
