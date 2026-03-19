// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using DurableTask.Core;
using DurableTask.Core.Exceptions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.DurableTask.Tests.V2;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class InstanceManagementTests : DurableTaskEndToEndTestBase
    {
        public InstanceManagementTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ContinueAsNew_Repro285()
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ContinueAsNew_Repro285),
                enableExtendedSessions: true))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ContinueAsNew_Repro285), 0, this.output);

                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output);

                await host.StopAsync();
            }
        }

        /// <summary>
        /// Test which validates that orchestrations can call a timer and then cancel it if receiving an event instead.
        /// This is meant to catch regressions of azure/durabletask/#285.
        /// </summary>
        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, 20)]
        [InlineData(false, 20)]
        public async Task ContinueAsNewMultipleTimersAndEvents(bool extendedSessions, int numSignals)
        {
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.ContinueAsNewMultipleTimersAndEvents),
                enableExtendedSessions: extendedSessions))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.ContinueAsNewMultipleTimersAndEvents), numSignals, this.output);

                await Task.Delay(TimeSpan.FromSeconds(2));

                for (int i = numSignals; i > 0; i--)
                {
                    await client.RaiseEventAsync($"signal{i}", this.output);
                }

                var status = await client.WaitForCompletionAsync(this.output, false, false, TimeSpan.FromSeconds(80));
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("ok", status.Output);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.FlakeyTestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task ExternalEvents_WithTaskHubName_MultipleNamesLooping(bool extendedSessions, string storageProvider)
        {
            var taskHubName1 = "MultipleNamesLooping1";
            var taskHubName2 = "MultipleNamesLooping2";
            using (ITestHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions, storageProviderType: storageProvider))
            using (ITestHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions, storageProviderType: storageProvider))
            {
                await host1.StartAsync();
                await host2.StartAsync();
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.Counter2), null, this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "World", this.output);
                taskHubName1 = client1.TaskHubName;
                var instanceId = client1.InstanceId;

                // Perform some operations
                await client2.RaiseEventAsync(taskHubName1, instanceId, "incr", null, this.output);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "incr", null, this.output);
                await client2.RaiseEventAsync(taskHubName1, instanceId, "done", null, this.output);

                // Make sure it actually completed
                var status = await client1.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal(2, (int)status.Output);

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Purge_Single_Instance_History(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Purge_Single_Instance_History),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                string instanceId = Guid.NewGuid().ToString();
                string message = GenerateMediumRandomStringPayload().ToString();
                TestDurableClient client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.EchoWithActivity), message, this.output, instanceId);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(2));
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

                DurableOrchestrationStatus orchestrationStatus = await client.GetStatusAsync(true);
                Assert.NotNull(orchestrationStatus);
                Assert.Equal(instanceId, orchestrationStatus.InstanceId);
                Assert.True(orchestrationStatus.History.Count > 0);

                int blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", instanceId);
                Assert.True(blobCount > 0);

                await client.InnerClient.PurgeInstanceHistoryAsync(instanceId);

                orchestrationStatus = await client.GetStatusAsync(true);
                Assert.Null(orchestrationStatus);

                blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", instanceId);
                Assert.Equal(0, blobCount);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatusAsync_MultipleInstances(string storageProvider)
        {
            const string testName = nameof(this.GetStatusAsync_MultipleInstances);
            using (ITestHost host = TestHelpers.GetJobHost(this.loggerProvider, testName, false, storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client1 = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 1, this.output);
                var client2 = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), 2, this.output);

                string firstInstanceId = client1.InstanceId;
                string secondInstanceId = client2.InstanceId;
                string thirdInstanceId = "00000000";

                var instanceIdList = new List<string> { firstInstanceId, secondInstanceId, thirdInstanceId };

                IList<DurableOrchestrationStatus> statusList = await client1.InnerClient.GetStatusAsync(
                    instanceIdList,
                    showHistory: false,
                    showHistoryOutput: false,
                    showInput: true);
                Assert.Equal("1", statusList[0].Input.ToString());
                Assert.Equal("2", statusList[1].Input.ToString());
                Assert.Null(statusList[2]);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task PurgeMultipleInstanceHistory(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
               this.loggerProvider,
               nameof(this.PurgeMultipleInstanceHistory),
               extendedSessions,
               storageProviderType: storageProvider))
            {
                await host.StartAsync();

                var client1 = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "foo", this.output);
                await client1.WaitForCompletionAsync(this.output);
                var client2 = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "bar", this.output);
                await client2.WaitForCompletionAsync(this.output);
                var client3 = await host.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "baz", this.output);
                await client3.WaitForCompletionAsync(this.output);

                string firstInstanceId = client1.InstanceId;
                string secondInstanceId = client2.InstanceId;
                string thirdInstanceId = client3.InstanceId;
                string fourthInstanceId = "00000000";
                var instanceIdList = new List<string> { firstInstanceId, secondInstanceId, thirdInstanceId, fourthInstanceId };

                var purgeResult = await client1.InnerClient.PurgeInstanceHistoryAsync(instanceIdList);

                Assert.Equal("3", purgeResult.InstancesDeleted.ToString());
                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Purge_All_History_By_TimePeriod(bool extendedSessions, string storageProvider)
        {
            string testName = nameof(this.Purge_All_History_By_TimePeriod);
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                extendedSessions,
                storageProviderType: storageProvider,
                autoFetchLargeMessages: false))
            {
                await host.StartAsync();

                DateTime startDateTime = DateTime.Now;

                string firstInstanceId = Guid.NewGuid().ToString();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 10, this.output, firstInstanceId);
                await client.WaitForCompletionAsync(this.output);

                var status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string secondInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 10, this.output, secondInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string thirdInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 10, this.output, thirdInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string fourthInstanceId = Guid.NewGuid().ToString();
                string message = GenerateMediumRandomStringPayload().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.EchoWithActivity), message, this.output, fourthInstanceId);
                await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(2));

                status = await client.InnerClient.GetStatusAsync(fourthInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.True(status.History.Count > 0);
                await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);

                int blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", fourthInstanceId);
                Assert.True(blobCount > 0);

                await client.InnerClient.PurgeInstanceHistoryAsync(
                    startDateTime,
                    DateTime.UtcNow,
                    new List<OrchestrationStatus>
                    {
                        OrchestrationStatus.Completed,
                        OrchestrationStatus.Terminated,
                        OrchestrationStatus.Failed,
                    });

                status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(fourthInstanceId, true);
                Assert.Null(status);

                blobCount = await GetBlobCount($"{client.TaskHubName.ToLowerInvariant()}-largemessages", fourthInstanceId);
                Assert.Equal(0, blobCount);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Purge_Partially_History_By_TimePeriod(bool extendedSessions, string storageProvider)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.Purge_Partially_History_By_TimePeriod),
                extendedSessions,
                storageProviderType: storageProvider))
            {
                await host.StartAsync();

                DateTime startDateTime = DateTime.Now;

                string firstInstanceId = Guid.NewGuid().ToString();
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 10, this.output, firstInstanceId);
                await client.WaitForCompletionAsync(this.output);

                var status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                DateTime endDateTime = DateTime.Now;
                await Task.Delay(200);

                string secondInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 10, this.output, secondInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                string thirdInstanceId = Guid.NewGuid().ToString();
                client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.FanOutFanIn), 10, this.output, thirdInstanceId);
                await client.WaitForCompletionAsync(this.output);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("Done", status.Output.Value<string>());
                Assert.True(status.History.Count > 0);

                await client.InnerClient.PurgeInstanceHistoryAsync(
                    startDateTime,
                    endDateTime,
                    new List<OrchestrationStatus>
                    {
                        OrchestrationStatus.Completed,
                        OrchestrationStatus.Terminated,
                        OrchestrationStatus.Failed,
                    });

                status = await client.InnerClient.GetStatusAsync(firstInstanceId, true);
                Assert.Null(status);

                status = await client.InnerClient.GetStatusAsync(secondInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(secondInstanceId, status.InstanceId);
                Assert.True(status.History.Count > 0);

                status = await client.InnerClient.GetStatusAsync(thirdInstanceId, true);
                Assert.NotNull(status);
                Assert.Equal(thirdInstanceId, status.InstanceId);
                Assert.True(status.History.Count > 0);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RestartOrchestator_IsSuccess(bool restartWithNewInstanceId)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.RestartOrchestator_IsSuccess),
                false))
            {
                await host.StartAsync();

                var instanceId = Guid.NewGuid().ToString();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "RestartAsyncTest", this.output, instanceId: instanceId);
                await client.WaitForCompletionAsync(this.output);

                var newInstanceId = await client.InnerClient.RestartAsync(instanceId, restartWithNewInstanceId: restartWithNewInstanceId);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                if (restartWithNewInstanceId)
                {
                    Assert.NotEqual(instanceId, newInstanceId);
                }
                else
                {
                    Assert.Equal(instanceId, newInstanceId);
                }

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("RestartAsyncTest", status.Input);

                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task RestartOrchestrator_ThrowsException()
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };
            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.RestartOrchestrator_ThrowsException),
                false))
            {
                await host.StartAsync();

                var nonExistentId = Guid.NewGuid().ToString();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);

                ArgumentException exception =
                    await Assert.ThrowsAsync<ArgumentException>(async () =>
                    {
                        await client.InnerClient.RestartAsync(nonExistentId);
                    });

                Assert.Contains(
                    $"No instance with ID '{nonExistentId}' was found.",
                    exception.Message);
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStatesAndSuspend))]
        public async Task OverridableStates_RunningStatusesCorrectlyDeduped_ForRestart(
            bool extendedSessions,
            string storageProvider,
            bool anyStateOverridable,
            bool suspend)
        {
            await this.OverridableStates_RunningStatusesCorrectlyDeduped(
                extendedSessions,
                storageProvider,
                anyStateOverridable,
                suspend,
                restart: true);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStates))]
        public async Task OverridableStates_TerminalStatusesAlwaysReusable_ForRestart(
            bool extendedSessions,
            string storageProvider,
            bool anyStateOverridable)
        {
            await this.OverridableStates_TerminalStatusesAlwaysReusable(
                extendedSessions,
                storageProvider,
                anyStateOverridable,
                restart: true);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task GetStatus_WithCondition(bool extendedSessions, string storageProvider)
        {
            var taskHubName1 = "GetStatus1";
            var taskHubName2 = "GetStatus2";
            await TestHelpers.DeleteTaskHubResources(taskHubName1, extendedSessions);
            await TestHelpers.DeleteTaskHubResources(taskHubName2, extendedSessions);
            using (ITestHost host1 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName1, extendedSessions, storageProviderType: storageProvider))
            using (ITestHost host2 = TestHelpers.GetJobHost(this.loggerProvider, taskHubName2, extendedSessions, storageProviderType: storageProvider))
            {
                await host1.StartAsync();
                await host2.StartAsync();
                var client1 = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "foo", this.output);
                var client2 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "bar", this.output);
                var client3 = await host2.StartOrchestratorAsync(nameof(TestOrchestrations.SayHelloWithActivity), "baz", this.output);

                taskHubName1 = client1.TaskHubName;
                taskHubName2 = client2.TaskHubName;

                var yesterday = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
                var tomorrow = DateTime.UtcNow.Add(TimeSpan.FromDays(1));

                var condition1 = new OrchestrationStatusQueryCondition
                {
                    RuntimeStatus = new List<OrchestrationRuntimeStatus>()
                        { OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Completed },
                    CreatedTimeFrom = yesterday,
                    CreatedTimeTo = tomorrow,
                    TaskHubNames = new List<string>() { taskHubName1 },
                };
                var condition2 = new OrchestrationStatusQueryCondition
                {
                    RuntimeStatus = new List<OrchestrationRuntimeStatus>()
                        { OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Completed },
                    CreatedTimeFrom = yesterday,
                    CreatedTimeTo = tomorrow,
                    TaskHubNames = new List<string>() { taskHubName2 },
                };

                // Make sure it actually completed
                await client1.WaitForCompletionAsync(this.output);
                await client2.WaitForCompletionAsync(this.output);
                await client3.WaitForCompletionAsync(this.output);

                // Perform some operations
                var result1 = await client1.GetStatusAsync(condition1, CancellationToken.None);
                var result2 = await client2.GetStatusAsync(condition2, CancellationToken.None);

                Assert.Single(result1.DurableOrchestrationState);
                Assert.Equal(2, result2.DurableOrchestrationState.Count());

                await host1.StopAsync();
                await host2.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task Dedupe_Default_NotRunning_ThrowsException(bool extendedSessions, string storageProvider)
        {
            var instanceId = "OverridableStatesDefaultTest_" + Guid.NewGuid().ToString("N");

            using (ITestHost host = TestHelpers.GetJobHost(
                 this.loggerProvider,
                 nameof(this.Dedupe_Default_NotRunning_ThrowsException),
                 extendedSessions,
                 storageProviderType: storageProvider))
            {
                await host.StartAsync();

                int initialValue = 0;

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output, instanceId: instanceId);

                // Wait for the instance to go into the Running state. This is necessary to ensure log validation consistency.
                await client.WaitForStartupAsync(this.output);

                TimeSpan waitTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 300 : 10);

                // Perform some operations
                await client.RaiseEventAsync("operation", "incr", this.output);
                await client.WaitForCustomStatusAsync(waitTimeout, this.output, 1);

                // Make sure it's still running and didn't complete early (or fail).
                var status = await client.GetStatusAsync();
                Assert.NotNull(status);
                Assert.True(
                    status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                    status.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);

                FunctionInvocationException exception =
                    await Assert.ThrowsAsync<FunctionInvocationException>(async () =>
                    {
                        await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output, instanceId: instanceId);
                    });

                Assert.Equal(
                    "An Orchestration instance with the status Running already exists.",
                    exception.InnerException.Message);

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStatesAndSuspend))]
        public async Task OverridableStates_RunningStatusesCorrectlyDeduped_ForStartNew(
            bool extendedSessions,
            string storageProvider,
            bool anyStateOverridable,
            bool suspend)
        {
            await this.OverridableStates_RunningStatusesCorrectlyDeduped(
                extendedSessions,
                storageProvider,
                anyStateOverridable,
                suspend,
                restart: false);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [MemberData(nameof(GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStates))]
        public async Task OverridableStates_TerminalStatusesAlwaysReusable_ForStartNew(bool extendedSessions, string storageProvider, bool anyStateOverridable)
        {
            await this.OverridableStates_TerminalStatusesAlwaysReusable(
                extendedSessions,
                storageProvider,
                anyStateOverridable,
                restart: false);
        }

        // This method returns an array of [bool extendedSessions, string storageProvider, bool anyStateOverridable, bool suspend]
        // It combines the GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStates with both true and false for suspend.
        public static IEnumerable<object[]> GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStatesAndSuspend()
        {
            foreach (object[] data in GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStates())
            {
                yield return new object[] { data[0], data[1], data[2], true };
                yield return new object[] { data[0], data[1], data[2], false };
            }
        }

        // This method returns an array of [bool extendedSessions, string storageProvider, bool anyStateOverridable]
        // It combines the TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions with both true and false for anyStateOverridable.
        public static IEnumerable<object[]> GetBooleanAndFullFeaturedStorageProviderOptionsWithOverridableStates()
        {
            foreach (object[] data in TestDataGenerator.GetBooleanAndFullFeaturedStorageProviderOptions())
            {
                yield return new object[] { data[0], data[1], true };
                yield return new object[] { data[0], data[1], false };
            }
        }

        private static StringBuilder GenerateMediumRandomStringPayload()
        {
            // Generate a medium random string payload
            const int TargetPayloadSize = 128 * 1024; // 128 KB
            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 {}/<>.-";
            var sb = new StringBuilder();
            var random = new Random();
            while (Encoding.Unicode.GetByteCount(sb.ToString()) < TargetPayloadSize)
            {
                for (int i = 0; i < 1000; i++)
                {
                    sb.Append(Chars[random.Next(Chars.Length)]);
                }
            }

            return sb;
        }

        // Counts blobs in a container for end-to-end validation.
        private static async Task<int> GetBlobCount(string containerName, string directoryName)
        {
            string storageConnectionString = TestHelpers.GetStorageConnectionString();
            BlobServiceClient blobServiceClient;
            try
            {
                blobServiceClient = new BlobServiceClient(storageConnectionString);
            }
            catch (ArgumentException)
            {
                return 0;
            }

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
#if NET10_0_OR_GREATER
            return await System.Linq.AsyncEnumerable.CountAsync(containerClient.GetBlobsAsync());
#else
            return await containerClient.GetBlobsAsync().CountAsync();
#endif
        }

        private async Task OverridableStates_RunningStatusesCorrectlyDeduped(
            bool extendedSessions,
            string storageProvider,
            bool anyStateOverridable,
            bool suspend,
            bool restart)
        {
            DurableTaskOptions options = new ()
            {
                OverridableExistingInstanceStates = anyStateOverridable ? OverridableStates.AnyState : OverridableStates.NonRunningStates,
            };

            string instanceId = Guid.NewGuid().ToString("N");

            using ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                restart ? nameof(this.OverridableStates_RunningStatusesCorrectlyDeduped_ForRestart)
                    : nameof(this.OverridableStates_RunningStatusesCorrectlyDeduped_ForStartNew),
                extendedSessions,
                storageProviderType: storageProvider,
                options: options);

            await host.StartAsync();

            int initialValue = 0;

            TestDurableClient client = await host.StartOrchestratorAsync(
                nameof(TestOrchestrations.Counter),
                initialValue,
                this.output,
                instanceId: instanceId);

            // Wait for the instance to go into the Running state. This is necessary to ensure log validation consistency.
            await client.WaitForStartupAsync(this.output);

            var waitTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 300 : 10);

            // Perform some operations
            await client.RaiseEventAsync("operation", "incr", this.output);
            await client.WaitForCustomStatusAsync(waitTimeout, this.output, 1);

            // Make sure it's still running and didn't complete early (or fail).
            DurableOrchestrationStatus status = await client.GetStatusAsync();
            Assert.NotNull(status);
            Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);

            if (suspend)
            {
                await client.SuspendAsync("suspend for test");
                DurableOrchestrationStatus suspendedStatus = await client.WaitForStatusChange(this.output, OrchestrationRuntimeStatus.Suspended);
                Assert.Equal(OrchestrationRuntimeStatus.Suspended, suspendedStatus.RuntimeStatus);
            }

            Exception exception = null;
            try
            {
                if (restart)
                {
                    await client.InnerClient.RestartAsync(instanceId, restartWithNewInstanceId: false);
                }
                else
                {
                    await host.StartOrchestratorAsync(nameof(TestOrchestrations.Counter), initialValue, this.output, instanceId: instanceId);
                }
            }
            catch (OrchestrationAlreadyExistsException caughtException)
            {
                exception = caughtException;
            }
            catch (FunctionInvocationException caughtException)
            {
                exception = caughtException;
            }

            await host.StopAsync();

            // If any state is reusable, confirm that there is evidence the existing orchestration was terminated before the new one was created
            if (anyStateOverridable && this.useTestLogger)
            {
                IReadOnlyCollection<LogMessage> durableTaskCoreLogs =
                    this.loggerProvider.CreatedLoggers.Single(l => l.Category == "DurableTask.Core").LogMessages;
                Assert.Contains(durableTaskCoreLogs, log => log.ToString().StartsWith($"{instanceId}: Orchestration completed with a 'Terminated' status"));
            }

            // Otherwise confirm that an exception was thrown when trying to create a new orchestration when one with a nonterminal status already exists
            else if (!anyStateOverridable)
            {
                Assert.NotNull(exception);
                if (restart)
                {
                    Assert.IsType<OrchestrationAlreadyExistsException>(exception);
                }
                else
                {
                    Assert.IsType<FunctionInvocationException>(exception);
                    var functionInvocationException = (FunctionInvocationException)exception;
                    Assert.NotNull(functionInvocationException.InnerException);
                    Assert.IsType<OrchestrationAlreadyExistsException>(functionInvocationException.InnerException);
                }
            }
        }

        private async Task OverridableStates_TerminalStatusesAlwaysReusable(
            bool extendedSessions,
            string storageProvider,
            bool anyStateOverridable,
            bool restart)
        {
            DurableTaskOptions options = new ()
            {
                OverridableExistingInstanceStates = anyStateOverridable ? OverridableStates.AnyState : OverridableStates.NonRunningStates,
            };

            string instanceIdBase = Guid.NewGuid().ToString("N");

            using ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                restart ? nameof(this.OverridableStates_TerminalStatusesAlwaysReusable_ForRestart)
                    : nameof(this.OverridableStates_TerminalStatusesAlwaysReusable_ForStartNew),
                extendedSessions,
                storageProviderType: storageProvider,
                options: options);
            await host.StartAsync();

            int initialValue = 0;

            // Test for all terminal statuses: Completed, Failed, Terminated
            foreach (OrchestrationRuntimeStatus terminalStatus in new[]
            {
                OrchestrationRuntimeStatus.Completed,
                OrchestrationRuntimeStatus.Failed,
                OrchestrationRuntimeStatus.Terminated,
            })
            {
                string instanceId = instanceIdBase + "_" + terminalStatus;

                TestDurableClient client;
                client = await host.StartOrchestratorAsync(
                    terminalStatus == OrchestrationRuntimeStatus.Failed
                        ? nameof(TestOrchestrations.ThrowOrchestrator) : nameof(TestOrchestrations.Counter),
                    terminalStatus == OrchestrationRuntimeStatus.Failed ? string.Empty : initialValue,
                    this.output,
                    instanceId: instanceId);

                await client.WaitForStartupAsync(this.output);
                DurableOrchestrationStatus status = null;

                if (terminalStatus == OrchestrationRuntimeStatus.Completed)
                {
                    await client.RaiseEventAsync("operation", "end", this.output);
                }
                else if (terminalStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    await client.TerminateAsync("test terminate");
                }

                status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);
                Assert.Equal(terminalStatus, status.RuntimeStatus);

                // Should always be able to start a new orchestration with the same instanceId
                if (restart)
                {
                    await client.InnerClient.RestartAsync(instanceId, restartWithNewInstanceId: false);
                }
                else
                {
                    await host.StartOrchestratorAsync(
                        terminalStatus == OrchestrationRuntimeStatus.Failed
                            ? nameof(TestOrchestrations.ThrowOrchestrator) : nameof(TestOrchestrations.Counter),
                        terminalStatus == OrchestrationRuntimeStatus.Failed ? string.Empty : initialValue,
                        this.output,
                        instanceId: instanceId);
                }
            }

            await host.StopAsync();
        }
    }
}
