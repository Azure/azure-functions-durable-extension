// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.SqlServer;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Sql
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

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerMetricsProvider"/> class that
        /// retrieves scaling metrics from the specified SQL orchestration service.
        /// </summary>
        /// <param name="service">The SQL orchestration service used to get metrics.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="service"/> is null.
        /// </exception>
        public SqlServerMetricsProvider(SqlOrchestrationService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Gets the latest SQL Server scaling metrics, including the recommended worker count. Results are cached for 5 seconds to reduce query load.
        /// </summary>
        /// <param name="previousWorkerCount">
        /// The previous number of workers, used to compare scaling recommendations (optional).
        /// </param>
        /// <returns>
        /// A <see cref="SqlServerScaleMetric"/> containing the recommended worker count.
        /// </returns>
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
