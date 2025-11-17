// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql
{
    /// <summary>
    /// The SQL Server implementation of ScalabilityProvider.
    /// Provides scale monitoring and target-based scaling for SQL Server backend.
    /// </summary>
    public class SqlServerScalabilityProvider : ScalabilityProvider
    {
        private readonly SqlOrchestrationService service;
        private readonly string connectionName;
        private readonly ILogger logger;

        private readonly object initLock = new object();
        private SqlServerMetricsProvider singletonSqlMetricsProvider;

        /// <summary>
        /// Creates a new <see cref="SqlServerScalabilityProvider"/> for managing
        /// scaling operations using a SQL Server–based orchestration service.
        /// </summary>
        /// <param name="service">The SQL orchestration service instance.</param>
        /// <param name="connectionName">The name of the SQL connection.</param>
        /// <param name="logger">The logger used for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="service"/> is null.
        /// </exception>
        public SqlServerScalabilityProvider(
            SqlOrchestrationService service,
            string connectionName,
            ILogger logger)
            : base("mssql", connectionName)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.connectionName = connectionName;
            this.logger = logger;
        }

        /// <summary>
        /// The app setting containing the SQL Server connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        internal SqlServerMetricsProvider GetMetricsProvider(
            string hubName,
            SqlOrchestrationService sqlOrchestrationService,
            ILogger logger)
        {
            return new SqlServerMetricsProvider(sqlOrchestrationService);
        }

        /// <inheritdoc/>
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out IScaleMonitor scaleMonitor)
        {
            lock (this.initLock)
            {
                if (this.singletonSqlMetricsProvider == null)
                {
                    // This is only called by the ScaleController, it doesn't run in the Functions Host process.
                    this.singletonSqlMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.service,
                        this.logger);
                }

                scaleMonitor = new SqlServerScaleMonitor(functionId, hubName, this.singletonSqlMetricsProvider);
                return true;
            }
        }

        /// <inheritdoc/>
        public override bool TryGetTargetScaler(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out ITargetScaler targetScaler)
        {
            lock (this.initLock)
            {
                if (this.singletonSqlMetricsProvider == null)
                {
                    this.singletonSqlMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.service,
                        this.logger);
                }

                targetScaler = new SqlServerTargetScaler(functionId, this.singletonSqlMetricsProvider);
                return true;
            }
        }
    }
}
