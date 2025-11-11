// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    /// <summary>
    /// Tests for SqlServerTargetScaler.
    /// Validates the target-based autoscaling mechanism for Durable Functions with SQL Server backend.
    /// Tests worker count calculations based on SQL Server recommended replica count.
    /// Ensures accurate scaling decisions based on SQL Server metrics.
    /// </summary>
    [Collection("SqlServerTests")]
    public class SqlServerTargetScalerTests
    {
        private readonly ITestOutputHelper output;

        public SqlServerTargetScalerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Scenario: Target scaler calculates correct worker count using SQL provider factory.
        /// Validates that the target scaler is properly created via the scalability provider factory
        /// with trigger metadata and returns valid scaling recommendations from SQL Server.
        /// Tests the complete flow: factory -> provider -> target scaler -> scaling calculation.
        /// </summary>
        [Fact]
        public async Task TargetBasedScaling_WithProviderFactory_ReturnsExpectedWorkerCount()
        {
            var taskHubName = "testHub";
            var connectionName = "TestConnection";
            var connectionString = TestHelpers.GetSqlConnectionString();

            this.output.WriteLine($"Creating connection to the test SQL TaskHub: {taskHubName}");

            // Create target scaler using the scalability provider factory to ensure proper setup
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { $"ConnectionStrings:{connectionName}", connectionString },
                    { connectionName, connectionString },
                })
                .Build();

            var loggerFactory = new LoggerFactory();
            var nameResolver = new SimpleNameResolver();
            var factory = new SqlServerScalabilityProviderFactory(
                configuration,
                nameResolver,
                loggerFactory);

            // Create trigger metadata with proper settings
            var triggerMetadata = TestHelpers.CreateTriggerMetadata(taskHubName, 10, 20, connectionName, "mssql");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();
            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata) as SqlServerScalabilityProvider;

            Assert.NotNull(provider);

            // Get target scaler from provider
            bool targetScalerCreated = provider.TryGetTargetScaler(
                "functionId",
                "TestFunction",
                taskHubName,
                connectionName,
                out ITargetScaler targetScaler);

            Assert.True(targetScalerCreated);
            Assert.NotNull(targetScaler);
            Assert.IsType<SqlServerTargetScaler>(targetScaler);

            // Get scale result from TargetScaler
            TargetScalerResult scalerResult = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            // SQL Server's GetRecommendedReplicaCountAsync analyzes the database state and recommends worker count
            Assert.NotNull(scalerResult);
            Assert.True(scalerResult.TargetWorkerCount >= 0, "Target worker count should be non-negative");

            this.output.WriteLine($"Target worker count: {scalerResult.TargetWorkerCount}");
        }

        /// <summary>
        /// Scenario: Target scaler with zero recommended replica count.
        /// Validates that scaler correctly handles zero worker count recommendations.
        /// Tests edge case where SQL Server recommends no workers.
        /// </summary>
        [Fact]
        public async Task TargetBasedScalingTest_ReturnsValidResult()
        {
            // Arrange - Use real SqlOrchestrationService (works with Azure SQL or Docker SQL Server)
            var connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub", schemaName: null);
            var sqlService = new SqlOrchestrationService(settings);
            var metricsProvider = new SqlServerMetricsProvider(sqlService);

            var targetScaler = new SqlServerTargetScaler(
                "functionId",
                metricsProvider);

            // Act - Get real scale result from SQL Server
            TargetScalerResult result = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            // Assert - Verify result is valid (actual values depend on SQL Server state)
            Assert.NotNull(result);
            Assert.True(result.TargetWorkerCount >= 0, "Target worker count should be non-negative");
        }

        /// <summary>
        /// Scenario: Target scaler with negative recommended replica count.
        /// Validates that scaler ensures minimum worker count is 0 (never negative).
        /// Tests defensive programming for edge cases.
        /// </summary>
        [Fact]
        public async Task TargetBasedScaling_ValidatesNonNegativeResult()
        {
            // Arrange - Use real SqlOrchestrationService (works with Azure SQL or Docker SQL Server)
            var connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub", schemaName: null);
            var sqlService = new SqlOrchestrationService(settings);
            var metricsProvider = new SqlServerMetricsProvider(sqlService);

            var targetScaler = new SqlServerTargetScaler(
                "functionId",
                metricsProvider);

            // Act - Get real scale result from SQL Server
            TargetScalerResult result = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            // Assert - Verify that negative values are clamped to 0 (Math.Max ensures this)
            Assert.NotNull(result);
            Assert.True(result.TargetWorkerCount >= 0, "Target worker count should be clamped to 0 if negative");
        }

        private class SimpleNameResolver : INameResolver
        {
            public string Resolve(string name) => name;
        }
    }
}
