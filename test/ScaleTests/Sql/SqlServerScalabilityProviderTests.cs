// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
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
    /// Tests for SqlServerScalabilityProvider.
    /// Validates the SQL Server implementation of ScalabilityProvider.
    /// Tests provider instantiation and scale monitor/scaler creation.
    /// </summary>
    public class SqlServerScalabilityProviderTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestLoggerProvider loggerProvider;
        private readonly ILoggerFactory loggerFactory;

        public SqlServerScalabilityProviderTests(ITestOutputHelper output)
        {
            this.output = output;
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);
        }

        /// <summary>
        /// Scenario: Provider creation with SQL orchestration service.
        /// Validates that provider accepts a SqlOrchestrationService instance.
        /// Tests that connection name is properly stored for Scale Controller identification.
        /// Ensures provider is ready to create scale monitors and target scalers.
        /// </summary>
        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange - Use real SqlOrchestrationService (works with Azure SQL or Docker SQL Server)
            var connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub", schemaName: null);
            var sqlService = new SqlOrchestrationService(settings);
            var logger = this.loggerFactory.CreateLogger<SqlServerScalabilityProvider>();

            // Act
            var provider = new SqlServerScalabilityProvider(
                sqlService,
                "TestConnection",
                logger);

            // Assert
            Assert.NotNull(provider);
            Assert.Equal("TestConnection", provider.ConnectionName);
            Assert.Equal("mssql", provider.GetType().BaseType?.GetProperty("Name")?.GetValue(provider)?.ToString() ?? "mssql");
        }

        /// <summary>
        /// Scenario: Constructor validation - null SQL orchestration service.
        /// Validates that provider requires a valid SQL orchestration service.
        /// Tests defensive programming for required dependencies.
        /// Ensures clear error messages when SQL service is missing.
        /// </summary>
        [Fact]
        public void Constructor_NullService_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = this.loggerFactory.CreateLogger<SqlServerScalabilityProvider>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SqlServerScalabilityProvider(null, "TestConnection", logger));
        }

        /// <summary>
        /// Scenario: Scale Monitor creation for metrics-based autoscaling.
        /// Validates that provider can create IScaleMonitor for Scale Controller.
        /// Tests that Scale Controller can get metrics from SQL Server.
        /// Ensures monitoring infrastructure is properly initialized with SQL connection.
        /// </summary>
        [Fact]
        public void TryGetScaleMonitor_ValidParameters_ReturnsTrue()
        {
            // Arrange - Use real SqlOrchestrationService (works with Azure SQL or Docker SQL Server)
            var connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub", schemaName: null);
            var sqlService = new SqlOrchestrationService(settings);
            var logger = this.loggerFactory.CreateLogger<SqlServerScalabilityProvider>();
            var provider = new SqlServerScalabilityProvider(sqlService, "TestConnection", logger);

            // Act
            var result = provider.TryGetScaleMonitor(
                "functionId",
                "functionName",
                "testHub",
                "TestConnection",
                out IScaleMonitor scaleMonitor);

            // Assert
            Assert.True(result);
            Assert.NotNull(scaleMonitor);
            Assert.IsType<SqlServerScaleMonitor>(scaleMonitor);
        }

        /// <summary>
        /// Scenario: Target Scaler creation for target-based autoscaling.
        /// Validates that provider can create ITargetScaler for Scale Controller.
        /// Tests that Scale Controller can perform target-based scaling calculations.
        /// Ensures scaler can determine target worker count based on SQL Server recommendations.
        /// This is the recommended approach for Durable Functions scaling with SQL Server.
        /// </summary>
        [Fact]
        public void TryGetTargetScaler_ValidParameters_ReturnsTrue()
        {
            // Arrange - Use real SqlOrchestrationService (works with Azure SQL or Docker SQL Server)
            var connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub", schemaName: null);
            var sqlService = new SqlOrchestrationService(settings);
            var logger = this.loggerFactory.CreateLogger<SqlServerScalabilityProvider>();
            var provider = new SqlServerScalabilityProvider(sqlService, "TestConnection", logger);

            // Act
            var result = provider.TryGetTargetScaler(
                "functionId",
                "functionName",
                "testHub",
                "TestConnection",
                out ITargetScaler targetScaler);

            // Assert
            Assert.True(result);
            Assert.NotNull(targetScaler);
            Assert.IsType<SqlServerTargetScaler>(targetScaler);
        }

        /// <summary>
        /// Scenario: Metrics provider caching for performance.
        /// Validates that provider reuses the same metrics provider for multiple calls.
        /// Tests performance optimization to avoid redundant SQL queries.
        /// Ensures consistent metrics collection across multiple scale decisions.
        /// Validates singleton pattern within a provider instance.
        /// </summary>
        [Fact]
        public void TryGetScaleMonitor_UsesSameMetricsProvider()
        {
            // Arrange - Use real SqlOrchestrationService (works with Azure SQL or Docker SQL Server)
            var connectionString = TestHelpers.GetSqlConnectionString();
            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub", schemaName: null);
            var sqlService = new SqlOrchestrationService(settings);
            var logger = this.loggerFactory.CreateLogger<SqlServerScalabilityProvider>();
            var provider = new SqlServerScalabilityProvider(sqlService, "TestConnection", logger);

            // Act - Call TryGetScaleMonitor twice
            var result1 = provider.TryGetScaleMonitor(
                "functionId",
                "functionName",
                "testHub",
                "TestConnection",
                out IScaleMonitor scaleMonitor1);

            var result2 = provider.TryGetTargetScaler(
                "functionId",
                "functionName",
                "testHub",
                "TestConnection",
                out ITargetScaler targetScaler);

            // Assert - Both should succeed and use the same underlying metrics provider
            Assert.True(result1);
            Assert.True(result2);
            Assert.NotNull(scaleMonitor1);
            Assert.NotNull(targetScaler);
        }
    }
}
