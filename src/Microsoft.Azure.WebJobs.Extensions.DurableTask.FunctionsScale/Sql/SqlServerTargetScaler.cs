// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.Sql
{
    /// <summary>
    /// Target-based scaler for SQL Server backend.
    /// Provides target worker count recommendations based on SQL Server orchestration service metrics.
    /// Note: This class should be kept in sync with SqlTargetScaler in DurableTask.SqlServer.AzureFunctions.
    /// </summary>
    public class SqlServerTargetScaler : ITargetScaler
    {
        private readonly SqlServerMetricsProvider sqlMetricsProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerTargetScaler"/> class.
        /// </summary>
        /// <param name="functionId">The ID of the function to scale.</param>
        /// <param name="sqlMetricsProvider">The SQL Server metrics provider.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="sqlMetricsProvider"/> is null.
        /// </exception>
        public SqlServerTargetScaler(string functionId, SqlServerMetricsProvider sqlMetricsProvider)
        {
            this.sqlMetricsProvider = sqlMetricsProvider ?? throw new ArgumentNullException(nameof(sqlMetricsProvider));

            // Scalers in Durable Functions are per function IDs. Scalers share the same sqlMetricsProvider in the same task hub.
            this.TargetScalerDescriptor = new TargetScalerDescriptor(functionId);
        }

        /// <summary>
        /// Gets the descriptor for this target scaler.
        /// </summary>
        public TargetScalerDescriptor TargetScalerDescriptor { get; }

        /// <summary>
        /// Retrieves the current scale result based on SQL Server metrics, returning the recommended number of workers for the task hub.
        /// </summary>
        /// <param name="context">The context for scaling evaluation.</param>
        /// <returns>The calculated <see cref="TargetScalerResult"/>.</returns>
        public async Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            SqlServerScaleMetric sqlScaleMetric = await this.sqlMetricsProvider.GetMetricsAsync();
            return new TargetScalerResult
            {
                TargetWorkerCount = Math.Max(0, sqlScaleMetric.RecommendedReplicaCount),
            };
        }
    }
}
