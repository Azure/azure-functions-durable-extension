// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.SqlServer;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Sql
{
    /// <summary>
    /// The SQL Server implementation of ScalabilityProvider.
    /// Provides scale monitoring and target-based scaling for SQL Server backend.
    /// </summary>
    public class SqlServerScalabilityProvider : ScalabilityProvider
    {
        private readonly SqlOrchestrationService service;
        private readonly string connectionName;

        private readonly object initLock = new object();
        private SqlServerMetricsProvider singletonSqlMetricsProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerScalabilityProvider"/> class.
        /// for managing scaling operations using a SQL Server-based orchestration service.
        /// </summary>
        /// <param name="service">The SQL orchestration service instance.</param>
        /// <param name="connectionName">The name of the SQL connection.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="service"/> is null.
        /// </exception>
        public SqlServerScalabilityProvider(
            SqlOrchestrationService service,
            string connectionName)
            : base("mssql", connectionName)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.connectionName = connectionName;
        }

        /// <summary>
        /// Gets the app setting containing the SQL Server connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        /// <inheritdoc/>
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out IScaleMonitor scaleMonitor)
        {
            lock (this.initLock)
            {
                if (this.singletonSqlMetricsProvider == null)
                {
                    this.singletonSqlMetricsProvider = new SqlServerMetricsProvider(this.service);
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
            string targetConnectionName,
            out ITargetScaler targetScaler)
        {
            lock (this.initLock)
            {
                if (this.singletonSqlMetricsProvider == null)
                {
                    this.singletonSqlMetricsProvider = new SqlServerMetricsProvider(this.service);
                }

                targetScaler = new SqlServerTargetScaler(functionId, this.singletonSqlMetricsProvider);
                return true;
            }
        }
    }
}
