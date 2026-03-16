// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using DurableTask.AzureStorage;
using FluentAssertions.Common;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Xunit.Abstractions;

namespace WebJobs.Extensions.DurableTask.Tests.V2
{
    public class TestCleanup
    {
        private readonly ITestOutputHelper output;

        private readonly TestLoggerProvider loggerProvider;

        public TestCleanup(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerProvider = new TestLoggerProvider(output);
        }

        /// <summary>
        /// Cleans up old task hubs in the CI storage account to prevent clutter.
        /// </summary>
        [Fact]
        [Trait("Category", PlatformSpecificHelpers.TestCategory)]
        public async Task CleanupOldAzureStorageTaskHubs()
        {
            // The CI should run at least once a day, so this timespan should prevent us from deleting
            // deterministic taskhubs while they are running, which causes those tests to fail.
            TimeSpan oldTaskHubDeletionThreshold = TimeSpan.FromHours(25);

            // An approximate limit to the number of taskhubs to delete to prevent test from taking to long.
            // Future test runs will clean up more.
            const int maxDeletedTaskHubs = 2000;
            string connectionString = TestHelpers.GetStorageConnectionString();
            var blobServiceClient = new BlobServiceClient(connectionString);

            this.output.WriteLine($"Using storage account: {blobServiceClient.AccountName}");

            var containers = blobServiceClient.GetBlobContainersAsync();
#if NET10_0_OR_GREATER
            // .NET 10 uses the BCL async LINQ helpers to avoid duplicate method definitions.
            var filtered = System.Linq.AsyncEnumerable.Where(containers, c => c.Name.Contains("-leases", StringComparison.Ordinal));
            var filteredByAge = System.Linq.AsyncEnumerable.Where(filtered, c => DateTimeOffset.UtcNow.Subtract(c.Properties.LastModified) > oldTaskHubDeletionThreshold);
            var selected = System.Linq.AsyncEnumerable.Select(filteredByAge, c => c.Name[..c.Name.IndexOf("-leases")]);
            var taken = System.Linq.AsyncEnumerable.Take(selected, maxDeletedTaskHubs);
            List<string> taskHubsToDelete = await System.Linq.AsyncEnumerable.ToListAsync(taken);
#else
            List<string> taskHubsToDelete = await containers
                .Where(c => c.Name.Contains("-leases", StringComparison.Ordinal))
                .Where(c => DateTimeOffset.UtcNow.Subtract(c.Properties.LastModified) > oldTaskHubDeletionThreshold)
                .Select(c => c.Name[..c.Name.IndexOf("-leases")])
                .Take(maxDeletedTaskHubs)
                .ToListAsync();
#endif

            await Task.WhenAll(taskHubsToDelete.Select(taskHub => this.DeleteTaskHub(taskHub, connectionString)));
        }

        private async Task DeleteTaskHub(string taskHub, string connectionString)
        {
            var settings = new AzureStorageOrchestrationServiceSettings()
            {
                TaskHubName = taskHub,
                StorageAccountClientProvider = new StorageAccountClientProvider(connectionString),
            };

            var service = new AzureStorageOrchestrationService(settings);
            await service.StartAsync();
            this.output.WriteLine($"Deleting task hub : {taskHub}");
            try
            {
                await service.DeleteAsync();
            }
            catch (Exception ex)
            {
                // Log error, but don't fail the test, as it can be cleaned up later.
                this.output.WriteLine($"Encountered exception deleting task hub: : {ex.ToString()}");
            }
        }
    }
}
