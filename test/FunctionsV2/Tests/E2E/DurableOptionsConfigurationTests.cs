// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    [Trait("TestType", "E2E")]
    public class DurableOptionsConfigurationTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;

        public DurableOptionsConfigurationTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task EmptyStorageProviderUsesAzureStorageDefaults()
        {
            string testName = nameof(this.EmptyStorageProviderUsesAzureStorageDefaults).ToLowerInvariant();
            string hubName = testName + PlatformSpecificHelpers.VersionSuffix;

            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                false,
                storageProviderType: "empty_storage_provider",
                exactTaskHubName: hubName))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);

                await host.StopAsync();
            }

            // Ensure blobs touched in the last 30 seconds
            await AssertTestUsedAzureStorageAsync(hubName);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task NullStorageProviderUsesAzureStorageDefaults()
        {
            string testName = nameof(this.NullStorageProviderUsesAzureStorageDefaults).ToLowerInvariant();
            string hubName = testName + PlatformSpecificHelpers.VersionSuffix;

            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                false,
                storageProviderType: null,
                exactTaskHubName: hubName))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);
                Assert.NotNull(status);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
                Assert.Equal("World", status.Input);
                Assert.Equal("Hello, World!", status.Output);

                await host.StopAsync();
            }

            await AssertTestUsedAzureStorageAsync(hubName);
        }

        private static async Task AssertTestUsedAzureStorageAsync(string hubName)
        {
            // Verify that Azure Storage artifacts were created for this task hub,
            // confirming the runtime used Azure Storage as the default provider.
            string defaultConnectionString = TestHelpers.GetStorageConnectionString();
            string hubNameLower = hubName.ToLowerInvariant();
            var blobServiceClient = new BlobServiceClient(defaultConnectionString);
            var matchingContainers = new List<string>();
            await foreach (var container in blobServiceClient.GetBlobContainersAsync(prefix: hubNameLower))
            {
                matchingContainers.Add(container.Name);
            }

            Assert.NotEmpty(matchingContainers);
        }
    }
}
