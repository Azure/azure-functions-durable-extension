// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DurableTask.Netherite;
using DurableTask.Netherite.Scaling;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Netherite;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Integration tests for the Netherite target scaler using real Azurite storage
    /// and the Event Hubs emulator. These tests pre-seed Azurite with taskhub parameters
    /// (blob) and partition load information (table), then verify the full scaling pipeline:
    /// factory -> provider -> target scaler -> metrics -> result.
    /// </summary>
    public class NetheriteTargetScalerIntegrationTests
    {
        private readonly ITestOutputHelper output;
        private readonly ILoggerFactory loggerFactory;

        public NetheriteTargetScalerIntegrationTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerFactory.AddProvider(new TestLoggerProvider(output));
        }

        /// <summary>
        /// End-to-end integration test that exercises the full Netherite scaling pipeline
        /// against real Azurite and Event Hubs emulator:
        ///
        /// 1. Pre-seeds Azurite blob storage with TaskhubParameters (partition count, GUID).
        /// 2. Pre-seeds the DurableTaskPartitions table in Azurite with per-partition load info
        ///    using Netherite's own AzureTableLoadPublisher (the same path a running service uses).
        /// 3. Creates the NetheriteScalabilityProviderFactory with real configuration pointing
        ///    to Azurite (storage) and the Event Hubs emulator (partition metrics).
        /// 4. Gets a provider and target scaler through the normal factory flow, which exercises
        ///    NetheriteScaleControllerConnectionResolver and settings.Validate().
        /// 5. Calls GetScaleResultAsync, which reads load info from the Azurite table and queries
        ///    Event Hubs partition positions from the emulator.
        /// 6. Asserts the scaler returns a non-zero target worker count.
        /// </summary>
        [Fact]
        public async Task TargetBasedScaling_WithRealEmulators_ReturnsExpectedWorkerCount()
        {
            string storageConnectionString = TestHelpers.GetStorageConnectionString();
            string eventHubsConnectionString = TestHelpers.GetNetheriteEventHubsConnectionString();

            string taskHubName = "testHub";
            int partitionCount = 4;
            Guid taskHubGuid = Guid.NewGuid();
            string containerName = taskHubName.ToLowerInvariant() + "-storage";

            // 1. Pre-seed Azurite blob storage with taskhub parameters
            this.output.WriteLine($"Setting up blob container '{containerName}' in Azurite...");
            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            var taskhubParams = new
            {
                TaskhubName = taskHubName,
                TaskhubGuid = taskHubGuid,
                CreationTimestamp = DateTime.UtcNow,
                StorageFormat = "1.0",
                PartitionCount = partitionCount,
            };
            string taskhubParamsJson = JsonConvert.SerializeObject(taskhubParams, Formatting.Indented);
            await UploadBlobAsync(containerClient, "taskhubparameters.json", taskhubParamsJson);
            this.output.WriteLine("Uploaded taskhubparameters.json");

            // 2. Pre-seed the DurableTaskPartitions table with partition load info.
            //    NetheriteMetricsProvider reads load info from this table (not blobs) by default,
            //    because LoadInformationAzureTableName defaults to "DurableTaskPartitions".
            var storageConnectionInfo = ConnectionInfo.FromStorageConnectionString(
                storageConnectionString, ConnectionResolver.ResourceType.TableStorage);
            var loadPublisher = new AzureTableLoadPublisher(storageConnectionInfo, "DurableTaskPartitions", taskHubName);
            await loadPublisher.CreateIfNotExistsAsync(CancellationToken.None);

            var loadInfos = new Dictionary<uint, PartitionLoadInfo>();
            for (uint i = 0; i < partitionCount; i++)
            {
                loadInfos[i] = new PartitionLoadInfo
                {
                    WorkItems = 0,
                    Activities = 500,
                    Timers = 0,
                    Requests = 0,
                    Outbox = 0,
                    InputQueuePosition = 0L,
                    CommitLogPosition = 0L,
                    WorkerId = "worker0",
                    LatencyTrend = "MMMMM",
                    MissRate = 0.0,
                    CachePct = 100,
                    CacheMB = 10.0,
                };
            }

            await loadPublisher.PublishAsync(loadInfos, CancellationToken.None);
            this.output.WriteLine($"Published load info for {partitionCount} partitions to DurableTaskPartitions table.");

            // 3. Create real configuration pointing to Azurite and Event Hubs emulator
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "AzureWebJobsStorage", storageConnectionString },
                    { "EventHubsConnection", eventHubsConnectionString },
                })
                .Build();

            // 4. Create the factory and get a provider through the normal flow
            var factory = new NetheriteScalabilityProviderFactory(configuration, this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata(
                taskHubName, 10, 10, "AzureWebJobsStorage,EventHubsConnection", "Netherite");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            this.output.WriteLine("Creating provider through factory (exercises connection resolver + Validate)...");
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);
            Assert.IsType<NetheriteScalabilityProvider>(provider);
            this.output.WriteLine($"Provider created. ConnectionName={provider.ConnectionName}");

            // 5. Get the target scaler
            bool scalerCreated = provider.TryGetTargetScaler(
                "functionId",
                "TestFunction",
                taskHubName,
                provider.ConnectionName,
                out ITargetScaler targetScaler);

            Assert.True(scalerCreated, "Expected TryGetTargetScaler to return true");
            try
            {
                Assert.NotNull(targetScaler);
                Assert.IsType<NetheriteTargetScaler>(targetScaler);
                this.output.WriteLine("Target scaler created successfully.");

                // 6. Get scaling result (reads load info from table + Event Hubs emulator)
                this.output.WriteLine("Calling GetScaleResultAsync...");
                TargetScalerResult result = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

                Assert.NotNull(result);
                this.output.WriteLine($"Target worker count: {result.TargetWorkerCount}");

                // With 4 partitions each having 500 activities and maxConcurrentActivities=10,
                // the scaler should recommend multiple workers.
                Assert.True(result.TargetWorkerCount > 0, "Expected target worker count > 0 for loaded partitions");
            }
            finally
            {
                // Cleanup
                await loadPublisher.DeleteIfExistsAsync(CancellationToken.None);
                await containerClient.DeleteIfExistsAsync();
                this.output.WriteLine("Cleaned up table entries and blob container.");
            }
        }

        private static async Task UploadBlobAsync(BlobContainerClient container, string blobPath, string content)
        {
            var blobClient = container.GetBlobClient(blobPath);
            var bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);
            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } });
        }
    }
}
