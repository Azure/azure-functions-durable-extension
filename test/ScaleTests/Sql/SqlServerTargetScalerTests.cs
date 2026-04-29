// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    [Collection("SqlServerTests")]
    public class SqlServerTargetScalerTests
    {
        private readonly ITestOutputHelper output;

        public SqlServerTargetScalerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Creates 20 pending orchestrations in a clean SQL task hub, then verifies the
        /// target scaler returns exactly 2 workers.
        /// Formula from dt.GetScaleRecommendation:
        ///   CEILING(liveInstances / maxOrchestrations) + CEILING(liveTasks / maxActivities)
        ///   = CEILING(20 / 10) + CEILING(0 / 20) = 2.
        /// </summary>
        [Fact]
        public async Task TargetBasedScaling_WithPendingOrchestrations_ReturnsExpectedWorkerCount()
        {
            var taskHubName = "testHub";
            var connectionName = "TestConnection";
            var connectionString = TestHelpers.GetSqlConnectionString();
            int maxConcurrentOrchestrators = 10;
            int maxConcurrentActivities = 20;
            int orchestrationsToCreate = 20;

            this.output.WriteLine($"Creating connection to the test SQL TaskHub: {taskHubName}");

            var settings = new SqlOrchestrationServiceSettings(connectionString, taskHubName)
            {
                CreateDatabaseIfNotExists = true,
            };

            var sqlService = new SqlOrchestrationService(settings);

            // Clean slate: delete and recreate the schema so leftover data doesn't affect the count
            await sqlService.DeleteAsync();
            await sqlService.CreateIfNotExistsAsync();

            this.output.WriteLine($"Creating {orchestrationsToCreate} pending orchestrations.");

            for (int i = 0; i < orchestrationsToCreate; i++)
            {
                var instance = new OrchestrationInstance
                {
                    InstanceId = $"TestOrchestration_{Guid.NewGuid():N}",
                    ExecutionId = Guid.NewGuid().ToString(),
                };

                await sqlService.CreateTaskOrchestrationAsync(
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

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"ConnectionStrings:{connectionName}", connectionString },
                    { connectionName, connectionString },
                })
                .Build();

            var loggerFactory = new LoggerFactory();
            var factory = new SqlServerScalabilityProviderFactory(
                configuration,
                loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata(
                taskHubName, maxConcurrentOrchestrators, maxConcurrentActivities, connectionName, "mssql");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);

            bool targetScalerCreated = provider.TryGetTargetScaler(
                "functionId",
                "TestFunction",
                taskHubName,
                connectionName,
                out ITargetScaler targetScaler);

            Assert.True(targetScalerCreated);
            Assert.NotNull(targetScaler);
            Assert.IsType<SqlServerTargetScaler>(targetScaler);

            TargetScalerResult scalerResult = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            Assert.NotNull(scalerResult);
            int expectedWorkerCount = (int)Math.Ceiling((double)orchestrationsToCreate / maxConcurrentOrchestrators);
            this.output.WriteLine($"Target worker count: {scalerResult.TargetWorkerCount}, expected: {expectedWorkerCount}");
            Assert.Equal(expectedWorkerCount, scalerResult.TargetWorkerCount);
        }
    }
}
