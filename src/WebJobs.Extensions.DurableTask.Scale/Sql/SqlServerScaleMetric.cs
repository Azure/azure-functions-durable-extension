// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.Sql
{
    /// <summary>
    /// Scale metrics for SQL Server backend.
    /// Contains the recommended replica count based on SQL Server orchestration service analysis.
    /// </summary>
    public class SqlServerScaleMetric : ScaleMetrics
    {
        /// <summary>
        /// Gets or sets the recommended number of worker replicas based on SQL Server metrics.
        /// </summary>
        public int RecommendedReplicaCount { get; set; }
    }
}




