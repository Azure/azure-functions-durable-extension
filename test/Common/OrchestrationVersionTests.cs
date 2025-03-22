// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
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
        [InlineData(null, "null")]
        [InlineData("", "''")]
        [InlineData("4.5.6-preview", "'4.5.6-preview'")]
        public async Task CanCheckOrchestrationVersion(string appVersion, string expectedContextVersion)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.CanCheckOrchestrationVersion),
                enableExtendedSessions: false,
                options: new DurableTaskOptions { AppVersion = appVersion }))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.GetOrchestrationVersion), null, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(1));

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                var expectedOutput = $"Orchestration: {expectedContextVersion}; Sub-orchestration: {expectedContextVersion}";
                Assert.Equal(expectedOutput, status.Output.ToString());
                await host.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task OriginalOrchestrationVersionPersists()
        {
            var taskHubName = TestHelpers.GetTaskHubNameFromTestName(nameof(this.OriginalOrchestrationVersionPersists), false);

            using ITestHost host1 = GetJobHost(appVersion: "1.0");
            await host1.StartAsync();
            var client = await host1.StartOrchestratorAsync(nameof(TestOrchestrations.GetOrchestrationVersion_AfterExternalEvent), null, this.output);
            var status = await client.WaitForCustomStatusAsync(TimeSpan.FromMinutes(1), this.output, "Waiting");
            Assert.Equal(OrchestrationRuntimeStatus.Running, status.RuntimeStatus);
            await host1.StopAsync();

            using ITestHost host2 = GetJobHost(appVersion: "2.0");
            await host2.StartAsync();
            await client.RaiseEventAsync("Resume", this.output);
            status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(1));
            Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
            await host2.StopAsync();

            var expectedOutput = $"Orchestration: '1.0'; Sub-orchestration: '1.0'";
            Assert.Equal(expectedOutput, status.Output.ToString());

            ITestHost GetJobHost(string appVersion)
            {
                return TestHelpers.GetJobHost(
                                this.loggerProvider,
                                nameof(this.OriginalOrchestrationVersionPersists),
                                enableExtendedSessions: false,
                                exactTaskHubName: taskHubName,
                                options: new DurableTaskOptions { AppVersion = appVersion });
            }
        }
    }
}
