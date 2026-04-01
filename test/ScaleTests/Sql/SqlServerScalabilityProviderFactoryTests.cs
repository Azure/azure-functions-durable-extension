// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Sql;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Tests
{
    [Collection("SqlServerTests")]
    public class SqlServerScalabilityProviderFactoryTests
    {
        private readonly TestLoggerProvider loggerProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly IConfiguration configuration;

        public SqlServerScalabilityProviderFactoryTests(ITestOutputHelper output)
        {
            this.loggerFactory = new LoggerFactory();
            this.loggerProvider = new TestLoggerProvider(output);
            this.loggerFactory.AddProvider(this.loggerProvider);

            string connectionString = TestHelpers.GetSqlConnectionString();

            var settings = new SqlOrchestrationServiceSettings(connectionString, "testHub")
            {
                CreateDatabaseIfNotExists = true,
            };

            var service = new SqlOrchestrationService(settings);
            service.CreateIfNotExistsAsync().GetAwaiter().GetResult();

            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "SQLDB_Connection", connectionString },
                { "TestConnection", connectionString },
            });
            this.configuration = configBuilder.Build();
        }

        /// <summary>
        /// Verifies that the factory creates a valid SqlServerScalabilityProvider
        /// when trigger metadata specifies storageProvider.type = "mssql", resolves
        /// the connection name, and retrieves the correct connection string from IConfiguration.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_WithTriggerMetadataAndMssqlType_ReturnsValidProvider()
        {
            var connectionName = "TestConnection";
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 15, 25, connectionName, "mssql");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);
            Assert.IsType<SqlServerScalabilityProvider>(provider);
            Assert.Equal(connectionName, provider.ConnectionName);

            var expectedConnectionString = TestHelpers.GetSqlConnectionString();
            var actualConnectionString = this.configuration.GetConnectionString(connectionName) ?? this.configuration[connectionName];
            Assert.Equal(expectedConnectionString, actualConnectionString);
        }

        /// <summary>
        /// Verifies that invalid concurrency settings (maxConcurrentOrchestratorFunctions = 0)
        /// cause the factory to throw InvalidOperationException.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_InvalidMaxConcurrent_ThrowsInvalidOperationException()
        {
            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 0, 20, "TestConnection", "mssql");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            Assert.Throws<InvalidOperationException>(() => factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// Verifies that the factory throws InvalidOperationException when the
        /// connection name in trigger metadata cannot be resolved from configuration.
        /// </summary>
        [Fact]
        public void GetScalabilityProvider_MissingConnectionString_ThrowsInvalidOperationException()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string>());
            var emptyConfig = configBuilder.Build();

            var factory = new SqlServerScalabilityProviderFactory(
                emptyConfig,
                this.loggerFactory);

            // Use a connection name that does not actually exist in the configuration.
            var triggerMetadata = TestHelpers.CreateTriggerMetadata("testHub", 10, 20, "NonExistentConnection", "mssql");
            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            Assert.Throws<InvalidOperationException>(() => factory.GetScalabilityProvider(metadata, triggerMetadata));
        }

        /// <summary>
        /// End-to-end integration test: creates a SQL provider via trigger metadata,
        /// then verifies both TargetScaler and ScaleMonitor return valid results
        /// from a real SQL Server instance.
        /// </summary>
        [Fact]
        public async Task TriggerMetadataWithMssqlType_CreatesSqlProvider_AndTargetScalerAndScaleMonitorWork()
        {
            var hubName = "testHub";
            var connectionName = "TestConnection";
            var triggerMetadata = TestHelpers.CreateTriggerMetadata(hubName, 10, 20, connectionName, "mssql");

            var storageProvider = triggerMetadata.Metadata["storageProvider"] as JObject;
            Assert.NotNull(storageProvider);
            Assert.Equal("mssql", storageProvider["type"]?.ToString());

            var factory = new SqlServerScalabilityProviderFactory(
                this.configuration,
                this.loggerFactory);

            var metadata = triggerMetadata.ExtractDurableTaskMetadata();

            var provider = factory.GetScalabilityProvider(metadata, triggerMetadata);

            Assert.NotNull(provider);
            Assert.IsType<SqlServerScalabilityProvider>(provider);
            Assert.Equal(connectionName, provider.ConnectionName);

            var connectionString = this.configuration.GetConnectionString(connectionName) ?? this.configuration[connectionName];
            Assert.NotNull(connectionString);
            Assert.NotEmpty(connectionString);

            bool targetScalerCreated = provider.TryGetTargetScaler(
                "functionId",
                "functionName",
                hubName,
                connectionName,
                out ITargetScaler targetScaler);

            Assert.True(targetScalerCreated);
            Assert.NotNull(targetScaler);
            Assert.IsType<SqlServerTargetScaler>(targetScaler);

            var targetScalerResult = await targetScaler.GetScaleResultAsync(new TargetScalerContext());

            Assert.NotNull(targetScalerResult);
            Assert.True(targetScalerResult.TargetWorkerCount >= 0, "Target worker count should be non-negative");

            bool scaleMonitorResult = provider.TryGetScaleMonitor(
                "functionId",
                "functionName",
                hubName,
                connectionName,
                out IScaleMonitor scaleMonitor);

            Assert.True(scaleMonitorResult);
            Assert.NotNull(scaleMonitor);
            Assert.IsType<SqlServerScaleMonitor>(scaleMonitor);

            var metrics = await scaleMonitor.GetMetricsAsync();

            Assert.NotNull(metrics);
            Assert.IsType<SqlServerScaleMetric>(metrics);
            var sqlMetrics = (SqlServerScaleMetric)metrics;
            Assert.True(sqlMetrics.RecommendedReplicaCount >= 0, "Recommended replica count should be non-negative");
        }
    }
}
