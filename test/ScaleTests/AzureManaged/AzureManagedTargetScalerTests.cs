// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Core.Query;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureManaged;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.DurableTask.AzureManagedBackend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    /// <summary>
    /// Validates that the target-based autoscaling mechanism produces correct worker counts
    /// based on pending/active work item metrics from the Azure Managed backend.
    /// </summary>
    public class AzureManagedTargetScalerTests
    {
        private readonly ITestOutputHelper output;

        public AzureManagedTargetScalerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Target scaler calculates correct worker count based on pending orchestrations.
        /// Validates that with 20 pending orchestrations and MaxConcurrentOrchestrators=2,
        /// the scaler returns 10 workers (20/2 = 10).
        /// </summary>
        [Fact]
        public async Task TargetBasedScaling_WithPendingOrchestrations_ReturnsExpectedWorkerCount()
        {
            var taskHubName = "default";
            var connectionString = TestHelpers.GetAzureManagedConnectionString();
            var options = AzureManagedOrchestrationServiceOptions.FromConnectionString(connectionString);
            options.TaskHubName = taskHubName;
            options.MaxConcurrentOrchestrationWorkItems = 2;
            options.MaxConcurrentActivityWorkItems = 2;
            options.MaxConcurrentEntityWorkItems = 2;

            this.output.WriteLine($"Creating connection to the test DTS TaskHub: {taskHubName}");

            var loggerFactory = new LoggerFactory();
            using var service = new AzureManagedOrchestrationService(options, loggerFactory);

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

            await Task.Delay(2000);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", connectionString },
                })
                .Build();

            var factory = new AzureManagedScalabilityProviderFactory(
                configuration,
                loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata(taskHubName, 2, 2, "DURABLE_TASK_SCHEDULER_CONNECTION_STRING", "azureManaged");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);
            Assert.True(provider is AzureManagedScalabilityProvider, "Expected AzureManagedScalabilityProvider from factory.");

            bool targetScalerCreated = provider.TryGetTargetScaler(
                "functionId",
                "TestFunction",
                taskHubName,
                "DURABLE_TASK_SCHEDULER_CONNECTION_STRING",
                out ITargetScaler targetScaler);

            Assert.True(targetScalerCreated);
            Assert.NotNull(targetScaler);
            Assert.IsType<AzureManagedTargetScaler>(targetScaler);

            var verifyResult = await service.GetOrchestrationWithQueryAsync(new OrchestrationQuery { RuntimeStatus = status }, CancellationToken.None);
            this.output.WriteLine($"Found {verifyResult.OrchestrationState?.Count ?? 0} orchestrations via query");

            await Task.Delay(3000);

            TargetScalerResult scalerResult = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            Assert.NotNull(scalerResult);
            this.output.WriteLine($"Target worker count: {scalerResult.TargetWorkerCount}");
            Assert.Equal(10, scalerResult.TargetWorkerCount);
        }
    }
}
