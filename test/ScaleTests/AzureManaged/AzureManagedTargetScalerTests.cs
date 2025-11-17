// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureManaged;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.DurableTask.AzureManagedBackend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    /// <summary>
    /// Tests for AzureManagedTargetScaler.
    /// Validates the target-based autoscaling mechanism for Durable Functions with Azure Managed backend.
    /// Tests worker count calculations based on Azure Managed metrics.
    /// Ensures accurate scaling decisions based on work item metrics.
    /// </summary>
    public class AzureManagedTargetScalerTests
    {
        private readonly ITestOutputHelper output;

        public AzureManagedTargetScalerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Scenario: Target scaler calculates correct worker count based on pending orchestrations.
        /// Validates that with 20 pending orchestrations and MaxConcurrentOrchestrators=2, 
        /// the scaler returns 10 workers (20/2 = 10).
        /// Tests the complete flow: create orchestrations -> set concurrency limits -> verify scaling calculation.
        /// </summary>
        [Fact]
        public async Task TargetBasedScaling_WithPendingOrchestrations_ReturnsExpectedWorkerCount()
        {
            var taskHubName = "default";
            var connectionString = TestHelpers.GetAzureManagedConnectionString();
            var options = AzureManagedOrchestrationServiceOptions.FromConnectionString(connectionString);
            options.TaskHubName = taskHubName;

            // This test only cares about max concurrent orchestrations
            options.MaxConcurrentOrchestrationWorkItems = 2;
            options.MaxConcurrentActivityWorkItems = 2;

            this.output.WriteLine($"Creating connection to the test DTS TaskHub: {taskHubName}");

            var loggerFactory = new LoggerFactory();
            var service = new AzureManagedOrchestrationService(options, loggerFactory);

            // Make 20 pending fake orchestration instances in the taskhub/

            var status = new List<OrchestrationStatus>
            {
                OrchestrationStatus.Pending,
                OrchestrationStatus.Running,
                OrchestrationStatus.Suspended,
            };

            var query = new OrchestrationQuery { RuntimeStatus = status };
            var result = await service.GetOrchestrationWithQueryAsync(query, CancellationToken.None);

            int existingCount = result.OrchestrationState?.Count ?? 0;
            int orchestrationsToCreate = Math.Max(0, 20 - existingCount);

            this.output.WriteLine($"Found {existingCount} existing orchestrations. Creating {orchestrationsToCreate} new ones.");

            // Create orchestration instances
            for (int i = 0; i < orchestrationsToCreate; i++)
            {
                var instance = new OrchestrationInstance
                {
                    InstanceId = $"TestOrchestration_{Guid.NewGuid():N}",
                    ExecutionId = Guid.NewGuid().ToString(),
                };

                await service.CreateTaskOrchestrationAsync(
                    new TaskMessage
                    {
                        OrchestrationInstance = instance,
                        Event = new ExecutionStartedEvent(-1, "TestInput")
                        {
                            OrchestrationInstance = instance,
                            Name = "TestOrchestration",
                            Version = "1.0",
                            Input = "TestInput",
                        },
                    });
            }

            // Wait a bit for orchestrations to be registered
            await Task.Delay(2000);

            // Create target scaler using the scalability provider factory to ensure proper setup
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", connectionString },
                })
                .Build();

            var nameResolver = new SimpleNameResolver();
            var factory = new AzureManagedScalabilityProviderFactory(
                configuration,
                nameResolver,
                loggerFactory);

            // Create trigger metadata with proper settings
            var triggerMetadata = TestHelpers.CreateTriggerMetadata(taskHubName, 2, 2, "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", "azureManaged");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata) as AzureManagedScalabilityProvider;

            Assert.NotNull(provider);

            // Get target scaler from provider
            bool targetScalerCreated = provider.TryGetTargetScaler(
                "functionId",
                "TestFunction",
                taskHubName,
                "DURABLE_TASK_SCHEDULER_CONNECTION_STRING",
                out ITargetScaler targetScaler);

            Assert.True(targetScalerCreated);
            Assert.NotNull(targetScaler);
            Assert.IsType<AzureManagedTargetScaler>(targetScaler);

            // Query orchestrations to trigger metrics update in DTS emulator
            // The emulator may need this query operation to refresh its internal state
            var verifyQuery = new OrchestrationQuery { RuntimeStatus = status };
            var verifyResult = await service.GetOrchestrationWithQueryAsync(verifyQuery, CancellationToken.None);
            int verifyCount = verifyResult.OrchestrationState?.Count ?? 0;
            this.output.WriteLine($"Found {verifyCount} orchestrations via query");

            // Wait for metrics to be available - DTS emulator may need additional time after query
            await Task.Delay(3000);

            // Get scale result from TargetScaler
            TargetScalerResult scalerResult = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            // With 20 orchestrations and MaxConcurrentOrchestrators=2, we expect 10 workers (20/2 = 10)
            Assert.NotNull(scalerResult);

            // The actual count might be slightly different due to existing orchestrations, but should be close to 10
            // We verify it's at least 5 (half of expected) to account for test variations
            this.output.WriteLine($"Target worker count: {scalerResult.TargetWorkerCount}");
            Assert.Equal(10, scalerResult.TargetWorkerCount);
        }

        private class SimpleNameResolver : INameResolver
        {
            public string Resolve(string name) => name;
        }
    }
}
