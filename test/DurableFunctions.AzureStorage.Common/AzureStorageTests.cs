// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Xunit;
using Xunit.Abstractions;

namespace DurableFunctions.AzureStorageEndToEnd
{
    public class AzureStorageTests : DurableTaskEndToEndTests
    {
        public AzureStorageTests(ITestOutputHelper output) : base(output)
        {
        }

        public override TestHelpers GetTestHelpers(ITestOutputHelper output)
        {
            return new TestHelpers(output);
        }

        [Theory]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        [Trait("Category", TestHelpers.DefaultTestCategory + "_BVT")]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task AzureStorage_BigReturnValue_Activity(bool extendedSessions, bool autoFetch)
        {
            string taskHub = nameof(this.AzureStorage_BigReturnValue_Activity);
            using (ITestHost host = this.testHelper.GetJobHost(taskHub, extendedSessions, autoFetchLargeMessages: autoFetch))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.CallActivity);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;
                var input = new StartOrchestrationArgs
                {
                    FunctionName = nameof(TestActivities.BigReturnValue),
                    Input = stringLength,
                };

                var client = await host.StartOrchestratorAsync(orchestrator, input, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                if (!autoFetch)
                {
                    await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);
                }
                else
                {
                    Assert.Equal(stringLength, ((string)status.Output).Length);
                }

                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task AzureStorage_BigReturnValue_Orchestrator(bool extendedSessions, bool autoFetch)
        {
            string taskHub = nameof(this.AzureStorage_BigReturnValue_Orchestrator);
            using (ITestHost host = this.testHelper.GetJobHost(taskHub, extendedSessions, autoFetchLargeMessages: autoFetch))
            {
                await host.StartAsync();

                var orchestrator = nameof(TestOrchestrations.BigReturnValue);

                // The expected maximum payload size is 60 KB.
                // Strings in Azure Storage are encoded in UTF-16, which is 2 bytes per character.
                int stringLength = (61 * 1024) / 2;

                var client = await host.StartOrchestratorAsync(orchestrator, stringLength, this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                if (!autoFetch)
                {
                    await ValidateBlobUrlAsync(client.TaskHubName, client.InstanceId, (string)status.Output);
                }
                else
                {
                    Assert.Equal(stringLength, ((string)status.Output).Length);
                }

                await host.StopAsync();
            }
        }


        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task AzureStorage_FirstRetryIntervalLimitHit_ThrowsException()
        {
            string orchestrationFunctionName = nameof(TestOrchestrations.SimpleActivityRetrySuccceds);

            using (var host = this.testHelper.GetJobHost(
                "AzureStorageFirstRetryIntervalException", // Need custom name so don't exceed 50 chars
                false))
            {
                await host.StartAsync();

                var firstRetryInterval = TimeSpan.FromDays(7);
                var maxRetryInterval = TimeSpan.FromDays(1);

                var client = await host.StartOrchestratorAsync(orchestrationFunctionName, (firstRetryInterval, maxRetryInterval), this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = status.Output.ToString();
                Assert.Contains("FirstRetryInterval", output);
            }
        }

        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task AzureStorage_MaxRetryIntervalLimitHit_ThrowsException()
        {
            string orchestrationFunctionName = nameof(TestOrchestrations.SimpleActivityRetrySuccceds);

            using (var host = this.testHelper.GetJobHost(
                "AzureStorageMaxRetryIntervalException", // Need custom name so don't exceed 50 chars
                false))
            {
                await host.StartAsync();

                var firstRetryInterval = TimeSpan.FromDays(1);
                var maxRetryInterval = TimeSpan.FromDays(7);

                var client = await host.StartOrchestratorAsync(orchestrationFunctionName, (firstRetryInterval, maxRetryInterval), this.output);

                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);

                string output = status.Output.ToString();
                Assert.Contains("MaxRetryInterval", output);
            }
        }

        /// <summary>
        /// End-to-end test which validates that bad input for task hub name throws instance of <see cref="ArgumentException"/>.
        /// </summary>
        [Theory]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        [Trait("Category", TestHelpers.DefaultTestCategory + "_BVT")]
        [InlineData("Task-Hub-Name-Test")]
        [InlineData("1TaskHubNameTest")]
        [InlineData("/TaskHubNameTest")]
        [InlineData("-taskhubnametest")]
        [InlineData("taskhubnametesttaskhubnametesttaskhubnametesttaskhubnametesttaskhubnametesttaskhubnametest")]
        public async Task TaskHubName_Throws_ArgumentException(string taskHubName)
        {
            ArgumentException argumentException =
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    using (var host = this.testHelper.GetJobHost(
                        taskHubName,
                        false,
                        exactTaskHubName: taskHubName + this.testHelper.GetTaskHubSuffix()))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

            Assert.NotNull(argumentException);
            Assert.Equal(
                argumentException.Message.Contains($"{taskHubName}V1")
                    ? $"Task hub name '{taskHubName}V1' should contain only alphanumeric characters, start with a letter, and have length between 3 and 45."
                    : $"Task hub name '{taskHubName}' should contain only alphanumeric characters, start with a letter, and have length between 3 and 45.",
                argumentException.Message);
        }

        /// <summary>
        /// Tests default and custom values for task hub name/>.
        /// </summary>
        [Theory]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        [InlineData(null, "TestSiteName", "Production")]
        [InlineData(null, "TestSiteName", null)]
        [InlineData("CustomName", "TestSiteName", "Production")]
        [InlineData("CustomName", "TestSiteName", null)]
        [InlineData("CustomName", "TestSiteName", "Test")]
        [InlineData("TestSiteName", "TestSiteName", "Test")]
        public void TaskHubName_HappyPath(string customHubName, string siteName, string slotName)
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", siteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", slotName);

                var options = new DurableTaskOptions();
                options.LocalRpcEndpointEnabled = false;

                var expectedHubName = siteName;

                if (customHubName != null)
                {
                    expectedHubName = customHubName;
                    options.HubName = customHubName;
                }

                using (var host = this.testHelper.GetJobHostWithOptions(options))
                {
                    Assert.Equal(expectedHubName, options.HubName);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }

        /// <summary>
        /// Tests default and custom values for task hub name/>.
        /// </summary>
        [Theory]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        [InlineData("Task-Hub-Name-Test", "TaskHubNameTest")]
        [InlineData("1TaskHubNameTest", "t1TaskHubNameTest")]
        [InlineData("-taskhubnametest", "taskhubnametest")]
        [InlineData("-1taskhubnametest", "t1taskhubnametest")]
        [InlineData("--------", "DefaultTaskHub")]
        [InlineData("bb", "bbHub")]
        public void TaskHubName_DefaultHubName_UseSanitized(string siteName, string expectedHubName)
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", siteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "Production");

                var options = new DurableTaskOptions();
                options.LocalRpcEndpointEnabled = false;

                using (var host = this.testHelper.GetJobHostWithOptions(options))
                {
                    Assert.Equal(expectedHubName, options.HubName);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }

        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task TaskHubName_AppSettingReference_InvalidTaskHub_ThrowsException()
        {
            string taskHubSettingName = "TaskHubName";
            string taskHubName = "Invalid-Task-Hub";
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            durableTaskOptions.HubName = $"%{taskHubSettingName}%";

            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { taskHubSettingName, taskHubName },
            });

            taskHubName += this.testHelper.GetTaskHubSuffix();
            ArgumentException argumentException =
                await Assert.ThrowsAsync<ArgumentException>(async () =>
                {
                    using (var host = this.testHelper.GetJobHost(
                        nameof(this.TaskHubName_Throws_ArgumentException),
                        false,
                        exactTaskHubName: taskHubName))
                    {
                        await host.StartAsync();
                        await host.StopAsync();
                    }
                });

            Assert.NotNull(argumentException);
            Assert.Equal(
                $"Task hub name '{taskHubName}' should contain only alphanumeric characters, start with a letter, and have length between 3 and 45.",
                argumentException.Message);
        }

        /// <summary>
        /// End-to-end test which validates the Rewind functionality.
        /// </summary>
        [Theory]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        [Trait("Category", TestHelpers.DefaultTestCategory + "_BVT")]
        [MemberData(nameof(TestDataGenerator.GetFullFeaturedStorageProviderOptions), MemberType = typeof(TestDataGenerator))]
        public async Task RewindOrchestration(string storageProvider)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloWithActivityForRewind),
            };

            string activityFunctionName = nameof(TestActivities.Hello);

            using (ITestHost host = this.testHelper.GetJobHost(
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

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("Hello, Catherine!", status?.Output);

                await host.StopAsync();

                this.testHelper.AssertLogMessageSequence(
                    this.output,
                    "RewindOrchestration",
                    client.InstanceId,
                    false /* filterOutReplayLogs */,
                    orchestratorFunctionNames,
                    activityFunctionName);
            }
        }

        /// <summary>
        /// Tests that an attempt to use a default task hub name while in a test slot will throw an exception <see cref="InvalidOperationException"/>.
        /// </summary>
        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task TaskHubName_DefaultNameNonProductionSlot_ThrowsException()
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "TestSiteName");
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "Test");
                DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
                durableTaskOptions.LocalRpcEndpointEnabled = false;

                InvalidOperationException exception =
                    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    {
                        using (var host = this.testHelper.GetJobHostWithOptions(
                            durableTaskOptions))
                        {
                            await host.StartAsync();
                            await host.StopAsync();
                        }
                    });

                Assert.NotNull(exception);
                Assert.Contains("Task Hub name must be specified in host.json when using slots", exception.Message);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }

        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public async Task TaskHubName_AppSettingReference_ValidTaskHub_UsesResolvedTaskHub()
        {
            string taskHubSettingName = "TaskHubName";
            string taskHubName = "ValidTaskHub";
            DurableTaskOptions durableTaskOptions = new DurableTaskOptions();
            durableTaskOptions.HubName = $"%{taskHubSettingName}%";

            var nameResolver = new SimpleNameResolver(new Dictionary<string, string>()
            {
                { taskHubSettingName, taskHubName },
            });

            using (var host = this.testHelper.GetJobHostWithOptions(
                durableTaskOptions,
                nameResolver: nameResolver))
            {
                await host.StartAsync();
                await host.StopAsync();
            }

            Assert.Equal(taskHubName, durableTaskOptions.HubName);
        }

        [Fact]
        [Trait("Category", TestHelpers.DefaultTestCategory)]
        public void TaskHubName_DefaultNameSiteTooLong_UsesSanitizedHubName()
        {
            string currSiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
            string currSlotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", new string('a', 100));
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", null);

                var options = new DurableTaskOptions();

                var expectedHubName = new string('a', 45);

                using (var host = this.testHelper.GetJobHostWithOptions(options))
                {
                    Assert.Equal(expectedHubName, options.HubName);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", currSiteName);
                Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", currSlotName);
            }
        }
    }
}
