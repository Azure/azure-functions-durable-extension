// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql
{
    /// <summary>
    /// Azure Functions scale monitor implementation for the Durable Functions SQL Server backend.
    /// Provides metrics-based autoscaling recommendations based on SQL Server metrics.
    /// </summary>
    public class SqlServerScaleMonitor : IScaleMonitor<SqlServerScaleMetric>
    {
        private static readonly ScaleStatus ScaleInVote = new ScaleStatus { Vote = ScaleVote.ScaleIn };
        private static readonly ScaleStatus NoScaleVote = new ScaleStatus { Vote = ScaleVote.None };
        private static readonly ScaleStatus ScaleOutVote = new ScaleStatus { Vote = ScaleVote.ScaleOut };

        private readonly SqlServerMetricsProvider metricsProvider;
        private int? previousWorkerCount = -1;

        public SqlServerScaleMonitor(string functionId, string taskHubName, SqlServerMetricsProvider sqlMetricsProvider)
        {
            // Scalers in Durable Functions are per function IDs. Scalers share the same sqlMetricsProvider in the same task hub.
            string id = $"{functionId}-DurableTask-SqlServer:{taskHubName ?? "default"}".ToLower(CultureInfo.InvariantCulture);

            this.Descriptor = new ScaleMonitorDescriptor(id: id, functionId: functionId);
            this.metricsProvider = sqlMetricsProvider ?? throw new ArgumentNullException(nameof(sqlMetricsProvider));
        }

        /// <inheritdoc />
        public ScaleMonitorDescriptor Descriptor { get; }

        /// <inheritdoc />
        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync() => await this.GetMetricsAsync();

        /// <inheritdoc />
        public async Task<SqlServerScaleMetric> GetMetricsAsync()
        {
            return await this.metricsProvider.GetMetricsAsync(this.previousWorkerCount);
        }

        /// <inheritdoc />
        ScaleStatus IScaleMonitor.GetScaleStatus(ScaleStatusContext context) =>
            this.GetScaleStatusCore(context.WorkerCount, context.Metrics.Cast<SqlServerScaleMetric>());

        /// <inheritdoc />
        public ScaleStatus GetScaleStatus(ScaleStatusContext<SqlServerScaleMetric> context) =>
            this.GetScaleStatusCore(context.WorkerCount, context.Metrics);

        private ScaleStatus GetScaleStatusCore(int currentWorkerCount, IEnumerable<SqlServerScaleMetric> metrics)
        {
            SqlServerScaleMetric mostRecentMetric = metrics.LastOrDefault();
            if (mostRecentMetric == null)
            {
                return NoScaleVote;
            }

            this.previousWorkerCount = currentWorkerCount;

            if (mostRecentMetric.RecommendedReplicaCount > currentWorkerCount)
            {
                return ScaleOutVote;
            }
            else if (mostRecentMetric.RecommendedReplicaCount < currentWorkerCount)
            {
                return ScaleInVote;
            }
            else
            {
                return NoScaleVote;
            }
        }
    }
}
