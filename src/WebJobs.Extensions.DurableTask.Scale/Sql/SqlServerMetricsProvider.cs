// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.SqlServer;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql
{
    /// <summary>
    /// Metrics provider for SQL Server backend scaling.
    /// Provides recommended replica count based on SQL Server orchestration service metrics.
    /// </summary>
    public class SqlServerMetricsProvider
    {
        private readonly SqlOrchestrationService service;
        private DateTime metricsTimeStamp = DateTime.MinValue;
        private SqlServerScaleMetric metrics;

        public SqlServerMetricsProvider(SqlOrchestrationService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public virtual async Task<SqlServerScaleMetric> GetMetricsAsync(int? previousWorkerCount = null)
        {
            // We only want to query the metrics every 5 seconds to avoid excessive SQL queries.
            if (this.metrics == null || DateTime.UtcNow >= this.metricsTimeStamp.AddSeconds(5))
            {
                // GetRecommendedReplicaCountAsync will write a trace if the recommendation results
                // in a worker count that is different from the worker count we pass in as an argument.
                int recommendedReplicaCount = await this.service.GetRecommendedReplicaCountAsync(
                    previousWorkerCount,
                    CancellationToken.None);

                this.metricsTimeStamp = DateTime.UtcNow;
                this.metrics = new SqlServerScaleMetric { RecommendedReplicaCount = recommendedReplicaCount };
            }

            return this.metrics;
        }
    }
}
