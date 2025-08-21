// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    public class TagsTests : IDisposable
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;

        public TagsTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        public void Dispose()
        {
        }

        [Theory]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        [InlineData(true, TestHelpers.AzureStorageProviderType)]
        [InlineData(false, TestHelpers.AzureStorageProviderType)]
        [InlineData(true, TestHelpers.EmulatorProviderType)]
        [InlineData(false, TestHelpers.EmulatorProviderType)]
        public async Task TestWithTags(bool extendedSessions, string storageProviderType)
        {
            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.OrchestrationWithTags),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                nameof(this.TestWithTags),
                extendedSessions,
                storageProviderType: storageProviderType))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output, tags: new Dictionary<string, string> { { "key1", "value1" } });
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal(true, status?.Output);

                var historyStatus = await client.GetStatusAsync(
                    showHistory: true,
                    showHistoryOutput: true,
                    showInput: true);

                Assert.NotNull(historyStatus.Tags);
                Assert.Contains(historyStatus.Tags, kvp => kvp.Key == "key1" && kvp.Value == "value1");

                await host.StopAsync();
            }
        }
    }
}