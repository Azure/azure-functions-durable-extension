// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
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
            string defaultConnectionString = TestHelpers.GetStorageConnectionString();

            string blobLeaseContainerName = $"{testName}v2-leases";
            CloudStorageAccount account = CloudStorageAccount.Parse(defaultConnectionString);
            CloudBlobClient blobClient = account.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(blobLeaseContainerName);

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
            CloudBlockBlob blob = blobContainer.GetBlockBlobReference($"default/{testName}v2-control-00");
            await blob.FetchAttributesAsync();
            DateTimeOffset lastModified = blob.Properties.LastModified.Value;
            DateTimeOffset expectedLastModifiedTimeThreshold = DateTimeOffset.UtcNow.AddSeconds(-30);
            Assert.True(lastModified > expectedLastModifiedTimeThreshold);
        }

        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task NullStorageProviderUsesAzureStorageDefaults()
        {
            string testName = nameof(this.NullStorageProviderUsesAzureStorageDefaults).ToLowerInvariant();
            string defaultConnectionString = TestHelpers.GetStorageConnectionString();

            string blobLeaseContainerName = $"{testName}v2-leases";
            CloudStorageAccount account = CloudStorageAccount.Parse(defaultConnectionString);
            CloudBlobClient blobClient = account.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(blobLeaseContainerName);

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

            // Ensure blobs touched in the last 30 seconds
            CloudBlockBlob blob = blobContainer.GetBlockBlobReference($"default/{testName}v2-control-00");
            await blob.FetchAttributesAsync();
            DateTimeOffset lastModified = blob.Properties.LastModified.Value;
            DateTimeOffset expectedLastModifiedTimeThreshold = DateTimeOffset.UtcNow.AddSeconds(-30);
            Assert.True(lastModified > expectedLastModifiedTimeThreshold);
        }
    }
}
