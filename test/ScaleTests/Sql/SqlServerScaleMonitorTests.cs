// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Tests
{
    /// <summary>
    /// Tests for SqlServerScaleMonitor.
    /// Validates the metrics-based autoscaling monitor for Durable Functions with SQL Server backend.
    /// Tests scale metrics collection, scale status determination, and scale recommendations.
    /// Ensures Scale Controller can make informed autoscaling decisions based on SQL Server metrics.
    /// </summary>
    public class SqlServerScaleMonitorTests
    {
        private readonly string hubName = "DurableTaskTriggerHubName";
        private readonly string functionId = "FunctionId";
        private readonly ITestOutputHelper output;
        private readonly LoggerFactory loggerFactory;
        private readonly TestLoggerProvider loggerProvider;
        private readonly SqlServerMetricsProvider metricsProvider;
        private readonly SqlServerScaleMonitor scaleMonitor;

        public SqlServerScaleMonitorTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);

            // Create real SqlOrchestrationService (works with Azure SQL or Docker SQL Server)
            var connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, this.hubName, schemaName: null);
            var sqlService = new SqlOrchestrationService(settings);
            
            // Create real metrics provider
            this.metricsProvider = new SqlServerMetricsProvider(sqlService);

            this.scaleMonitor = new SqlServerScaleMonitor(
                this.functionId,
                this.hubName,
                this.metricsProvider);
        }

        /// <summary>
        /// Scenario: Scale Monitor descriptor creation.
        /// Validates that monitor descriptor has correct ID format.
        /// Tests that monitor can be identified by Scale Controller.
        /// </summary>
        [Fact]
        public void ScaleMonitorDescriptor_ReturnsExpectedValue()
        {
            Assert.Equal($"{this.functionId}-DurableTask-SqlServer:{this.hubName}".ToLower(), this.scaleMonitor.Descriptor.Id);
            Assert.Equal(this.functionId, this.scaleMonitor.Descriptor.FunctionId);
        }

        /// <summary>
        /// Scenario: Scale metrics collection with recommended replica count.
        /// Validates that monitor correctly retrieves metrics from SQL Server.
        /// Tests recommended replica count is properly captured.
        /// </summary>
        [Fact]
        public async Task GetMetrics_ReturnsExpectedResult()
        {
            // Act - Get real metrics from SQL Server (works with Azure SQL or Docker SQL Server)
            SqlServerScaleMetric metric = await this.scaleMonitor.GetMetricsAsync();

            // Assert - Verify metrics are returned (actual values depend on SQL Server state)
            Assert.NotNull(metric);
            Assert.True(metric.RecommendedReplicaCount >= 0, "Recommended replica count should be non-negative");
        }

        /// <summary>
        /// Scenario: Scale status - Scale Out vote.
        /// Validates that monitor votes to scale out when recommended replica count exceeds current worker count.
        /// Tests scaling up scenarios based on SQL Server recommendations.
        /// </summary>
        [Fact]
        public void GetScaleStatus_RecommendedCountGreaterThanCurrent_ReturnsScaleOut()
        {
            // Arrange
            int currentWorkerCount = 3;
            int recommendedReplicaCount = 10;
            var metrics = new List<SqlServerScaleMetric>
            {
                new SqlServerScaleMetric { RecommendedReplicaCount = recommendedReplicaCount },
            };

            var context = new ScaleStatusContext<SqlServerScaleMetric>
            {
                WorkerCount = currentWorkerCount,
                Metrics = metrics,
            };

            // Act
            ScaleStatus status = this.scaleMonitor.GetScaleStatus(context);

            // Assert
            Assert.Equal(ScaleVote.ScaleOut, status.Vote);
        }

        /// <summary>
        /// Scenario: Scale status - Scale In vote.
        /// Validates that monitor votes to scale in when recommended replica count is less than current worker count.
        /// Tests scaling down scenarios based on SQL Server recommendations.
        /// </summary>
        [Fact]
        public void GetScaleStatus_RecommendedCountLessThanCurrent_ReturnsScaleIn()
        {
            // Arrange
            int currentWorkerCount = 10;
            int recommendedReplicaCount = 3;
            var metrics = new List<SqlServerScaleMetric>
            {
                new SqlServerScaleMetric { RecommendedReplicaCount = recommendedReplicaCount },
            };

            var context = new ScaleStatusContext<SqlServerScaleMetric>
            {
                WorkerCount = currentWorkerCount,
                Metrics = metrics,
            };

            // Act
            ScaleStatus status = this.scaleMonitor.GetScaleStatus(context);

            // Assert
            Assert.Equal(ScaleVote.ScaleIn, status.Vote);
        }

        /// <summary>
        /// Scenario: Scale status - No Scale vote.
        /// Validates that monitor votes for no scale when recommended replica count equals current worker count.
        /// Tests stable state scenarios.
        /// </summary>
        [Fact]
        public void GetScaleStatus_RecommendedCountEqualsCurrent_ReturnsNoScale()
        {
            // Arrange
            int currentWorkerCount = 5;
            int recommendedReplicaCount = 5;
            var metrics = new List<SqlServerScaleMetric>
            {
                new SqlServerScaleMetric { RecommendedReplicaCount = recommendedReplicaCount },
            };

            var context = new ScaleStatusContext<SqlServerScaleMetric>
            {
                WorkerCount = currentWorkerCount,
                Metrics = metrics,
            };

            // Act
            ScaleStatus status = this.scaleMonitor.GetScaleStatus(context);

            // Assert
            Assert.Equal(ScaleVote.None, status.Vote);
        }

        /// <summary>
        /// Scenario: Scale status with empty metrics.
        /// Validates that monitor returns NoScale when no metrics are available.
        /// Tests graceful handling of empty metric collection.
        /// </summary>
        [Fact]
        public void GetScaleStatus_EmptyMetrics_ReturnsNoScale()
        {
            // Arrange
            var emptyMetrics = new List<SqlServerScaleMetric>();
            var context = new ScaleStatusContext<SqlServerScaleMetric>
            {
                WorkerCount = 5,
                Metrics = emptyMetrics,
            };

            // Act
            ScaleStatus status = this.scaleMonitor.GetScaleStatus(context);

            // Assert
            Assert.Equal(ScaleVote.None, status.Vote);
        }

        /// <summary>
        /// Scenario: Scale status uses most recent metric.
        /// Validates that monitor uses the last metric in the collection for decision making.
        /// Tests that historical metrics don't override current recommendations.
        /// </summary>
        [Fact]
        public void GetScaleStatus_UsesMostRecentMetric()
        {
            // Arrange - Multiple metrics, should use the last one
            var metrics = new List<SqlServerScaleMetric>
            {
                new SqlServerScaleMetric { RecommendedReplicaCount = 10 }, // Older
                new SqlServerScaleMetric { RecommendedReplicaCount = 3 },  // Older
                new SqlServerScaleMetric { RecommendedReplicaCount = 5 },  // Most recent
            };

            var context = new ScaleStatusContext<SqlServerScaleMetric>
            {
                WorkerCount = 5,
                Metrics = metrics,
            };

            // Act
            ScaleStatus status = this.scaleMonitor.GetScaleStatus(context);

            // Assert - Should use the most recent metric (5), which equals current worker count
            Assert.Equal(ScaleVote.None, status.Vote);
        }
    }
}
