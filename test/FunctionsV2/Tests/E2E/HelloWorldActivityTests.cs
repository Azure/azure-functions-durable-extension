// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class HelloWorldActivityTests : DurableTaskEndToEndTestBase
    {
        public HelloWorldActivityTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// End-to-end test which runs a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_Activity(bool extendedSessions, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_Activity), extendedSessions, storageProvider);
        }

        /// <summary>
        /// End-to-end test which validates logs for replay events by a simple orchestrator function that calls a single activity function.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_ValidateReplayEventLogs(bool traceReplayEvents, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_ValidateReplayEventLogs), false, storageProvider, traceReplayEvents: traceReplayEvents);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_Activity_History(bool extendedSessions, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_Activity_History), extendedSessions, storageProvider, showHistory: true);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that history information with input and result date is provided.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_ShowHistoryInputOutput(bool extendedSessions, string storageProvider)
        {
            await this.HelloWorldOrchestration_Activity_Main_Logic(nameof(this.HelloWorldOrchestration_ShowHistoryInputOutput), extendedSessions, storageProvider, showHistory: true, showHistoryOutput: true);
        }

        /// <summary>
        ///  End-to-end test which runs a simple orchestrator function that calls a single activity function and verifies that the generated GUID-s from the DurableOrchestrationContext are the same.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldActivityWithNewGUID(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityWithDeterministicGuid),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldActivityWithNewGUID),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status =
                    await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("True", status.Output.ToString());
            }
        }

        /// <summary>
        ///  End-to-end test which  validates that <see cref="DurableOrchestrationContext"/> NewGuid method creates unique GUIDs.
        ///  The tests creates 10,000 GUIDs and validates that all the values are unique.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task VerifyUniqueGuids(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.VerifyUniqueGuids),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.VerifyUniqueGuids),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                DurableOrchestrationStatus status =
                    await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Empty(status.Input.ToString());
                Assert.Equal("True", status.Output.ToString());
            }
        }

        /// <summary>
        ///  End-to-end test which  validates that <see cref="DurableOrchestrationContext"/> NewGuid method creates the same GUIDs on replay.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task VerifySameGuidsOnReplay(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.VerifySameGuidGeneratedOnReplay),
            };

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.VerifySameGuidsOnReplay),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], null, this.output);
                DurableOrchestrationStatus status =
                    await client.WaitForCompletionAsync(this.output);

                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Empty(status.Input.ToString());
                Assert.Equal("True", status.Output.ToString());
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task HelloWorldOrchestration_Activity_CustomStatus(bool extendedSessions, string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityAndCustomStatus),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.HelloWorldOrchestration_Activity_CustomStatus),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(this.output);

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
        /// End-to-end test which validates fire-and-forget of a suborchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task FireAndForgetSuborchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.FireAndForgetSuborchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FireAndForgetHelloOrchestration), null, this.output);

                // Wait for it to complete
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                string subOrchestrationInstanceId = (string)status.Output;

                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

                do
                {
                    status = await client.InnerClient.GetStatusAsync(subOrchestrationInstanceId);
                    await Task.Delay(50);
                }
                while (DateTime.UtcNow <= deadline
                        && (status == null || status.RuntimeStatus != OrchestrationRuntimeStatus.Completed));

                Assert.NotNull(status);
                Assert.Equal("Hello, Heloise!", (string)status!.Output);
                await host.StopAsync();
            }
        }

        /// <summary>
        /// End-to-end test which validates function chaining by implementing a naive factorial function orchestration.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task SequentialOrchestration(bool extendedSessions, string storageProvider)
        {
            string instanceId;
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.SequentialOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Factorial), 10, this.output);
                instanceId = client.InstanceId;

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(10, status.Input);
                Assert.Equal(3628800, status.Output);

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
        [MemberData(nameof(TestDataGenerator.GetAllSupportedExtendedSessionWithStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ParallelOrchestration(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ParallelOrchestration),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.DiskUsage), Environment.CurrentDirectory, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromSeconds(90));
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(Environment.CurrentDirectory, status.Input);
                Assert.True((long?)status.Output > 0L);

                await host.StopAsync();
            }
        }

        private async Task HelloWorldOrchestration_Activity_Main_Logic(string taskHubName, bool extendedSessions, string storageProvider, bool showHistory = false, bool showHistoryOutput = false, bool traceReplayEvents = true)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivity),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                taskHubName,
                extendedSessions,
                traceReplayEvents: traceReplayEvents,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                DurableOrchestrationStatus status = await client.WaitForCompletionAsync(this.output, showHistory, showHistoryOutput);

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
                        Assert.NotNull(status.History[0]["Input"]);
                        Assert.NotNull(status.History[1]["Result"]);
                        Assert.Equal("Hello, World!", status.History[1]["Result"].ToString());
                        Assert.NotNull(status.History[2]["Result"]);
                        Assert.Equal("Hello, World!", status.History[2]["Result"].ToString());
                    }
                    else
                    {
                        Assert.NotNull(status.History[0]["Input"]);
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
                        extendedSessions || !traceReplayEvents,
                        orchestratorFunctionNames,
                        activityFunctionName);
                }
            }
        }
    }
}
