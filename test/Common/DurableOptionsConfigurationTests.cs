// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
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

            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                false,
                storageProviderType: "empty_storage_provider"))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();
            }

            // Ensure blobs touched in the last 30 seconds
            await AssertTestUsedAzureStorageAsync(testName);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task NullStorageProviderUsesAzureStorageDefaults()
        {
            string testName = nameof(this.NullStorageProviderUsesAzureStorageDefaults).ToLowerInvariant();

            string[] orchestratorFunctionNames =
            {
                nameof(TestOrchestrations.SayHelloInline),
            };

            using (var host = TestHelpers.GetJobHost(
                this.loggerProvider,
                testName,
                false,
                storageProviderType: null))
            {
                await host.StartAsync();

                var client = await host.StartOrchestratorAsync(orchestratorFunctionNames[0], "World", this.output);
                var status = await client.WaitForCompletionAsync(this.output);

                Assert.Equal(OrchestrationRuntimeStatus.Completed, status?.RuntimeStatus);
                Assert.Equal("World", status?.Input);
                Assert.Equal("Hello, World!", status?.Output);

                await host.StopAsync();
            }

            await AssertTestUsedAzureStorageAsync(testName);
        }

        private static async Task AssertTestUsedAzureStorageAsync(string testName)
        {
            // Ensure blobs touched in the last 30 seconds
            string defaultConnectionString = TestHelpers.GetStorageConnectionString();
            string blobLeaseContainerName = $"{testName}{PlatformSpecificHelpers.VersionSuffix.ToLower()}-leases";
            CloudStorageAccount account = CloudStorageAccount.Parse(defaultConnectionString);
            CloudBlobClient blobClient = account.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(blobLeaseContainerName);
            CloudBlockBlob blob = blobContainer.GetBlockBlobReference($"default/{testName}{PlatformSpecificHelpers.VersionSuffix.ToLower()}-control-00");
            await blob.FetchAttributesAsync();
            DateTimeOffset lastModified = blob.Properties.LastModified.Value;
            DateTimeOffset expectedLastModifiedTimeThreshold = DateTimeOffset.UtcNow.AddSeconds(-30);
            Assert.True(lastModified > expectedLastModifiedTimeThreshold);
        }
    }
}
