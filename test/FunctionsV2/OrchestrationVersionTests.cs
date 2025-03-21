// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
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
                Assert.Equal(expectedContextVersion, status.Output.ToString());
                await host.StopAsync();
            }
        }
    }
}
