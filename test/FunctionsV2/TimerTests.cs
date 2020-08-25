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
    public class TimerTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public TimerTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LongRunningTimer(bool extendedSessions)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.LongRunningTimer),
                extendedSessions,
                storageProviderType: "azure_storage",
                durabilityProviderFactoryType: typeof(AzureStorageShortenedTimerDurabilityProviderFactory)))
            {
                await host.StartAsync();

                var fireAt = DateTime.UtcNow.AddMinutes(2);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Timer), fireAt, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(3));

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                await host.StopAsync();
            }
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TimerLengthLessThanMaxTime(bool extendedSessions)
        {
            using (ITestHost host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.LongRunningTimer),
                extendedSessions,
                storageProviderType: "azure_storage",
                durabilityProviderFactoryType: typeof(AzureStorageShortenedTimerDurabilityProviderFactory)))
            {
                await host.StartAsync();

                var fireAt = DateTime.UtcNow.AddSeconds(30);
                var client = await host.StartOrchestratorAsync(nameof(TestOrchestrations.Timer), fireAt, this.output);
                var status = await client.WaitForCompletionAsync(this.output, timeout: TimeSpan.FromMinutes(2));

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                await host.StopAsync();
            }
        }
    }
}
