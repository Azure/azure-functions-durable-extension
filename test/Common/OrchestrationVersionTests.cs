// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class OrchestrationVersionTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public OrchestrationVersionTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1.0")]
        [InlineData("4.5.6-preview")]
        public async Task OrchestrationVersionIsDeterminedByHostDefaultVersion(string defaultVersion)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.OrchestrationVersionIsDeterminedByHostDefaultVersion),
                enableExtendedSessions: false,
                options: new DurableTaskOptions { DefaultVersion = defaultVersion }))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.GetOrchestrationVersion), null, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(1));

                var expectedContextVersion = JsonSerializer.Serialize(defaultVersion);
                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                var expectedOutput = $"Orchestration: {expectedContextVersion}; Sub-orchestration: {expectedContextVersion}; Sub-orchestration from entity: {expectedContextVersion}";
                Assert.Equal(expectedOutput, status.Output.ToString());
                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task ExistingOrchestrationVersionIsImmutable()
        {
            var taskHubName = TestHelpers.GetTaskHubNameFromTestName(nameof(this.ExistingOrchestrationVersionIsImmutable), false);

            // Start an orchestration on a host with defaultVersion set to 1.0, and wait until it's paused.
            using ITestHost host1 = GetJobHost(taskHubName, defaultVersion: "1.0");
            await host1.StartAsync();
            var client = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.GetOrchestrationVersion_AfterExternalEvent), null, this.output);
            var status = await client.WaitForCustomStatusAsync(TimeSpan.FromMinutes(1), this.output, "Waiting");
            Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);
            await host1.StopAsync();

            // Resume the same orchestration on a host with defaultVersion set to 2.0.
            using ITestHost host2 = GetJobHost(taskHubName, defaultVersion: "2.0");
            await host2.StartAsync();
            await client.RaiseEventAsync("Resume", this.output);
            status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(1));
            Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
            await host2.StopAsync();

            // The original orchestration version (1.0) persists. However, this version
            // is *not* propagated to the sub-orchestration started when this orchestration
            // was already running on the host with defaultVersion set to 2.0.
            var expectedOutput = $"Orchestration: \"1.0\"; Sub-orchestration: \"2.0\"; Sub-orchestration from entity: \"2.0\"";
            Assert.Equal(expectedOutput, status.Output.ToString());

            ITestHost GetJobHost(string taskHubName, string defaultVersion)
            {
                return TestHelpers.GetJobHost(
                                this.loggerProvider,
                                nameof(this.ExistingOrchestrationVersionIsImmutable),
                                enableExtendedSessions: false,
                                exactTaskHubName: taskHubName,
                                options: new DurableTaskOptions { DefaultVersion = defaultVersion });
            }
        }
    }
}
