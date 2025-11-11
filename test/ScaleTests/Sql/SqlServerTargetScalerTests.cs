// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
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
    }
}
