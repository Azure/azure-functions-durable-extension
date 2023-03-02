// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// ITelemetryActivator is an interface.
    /// </summary>
    public interface ITelemetryActivator
    {
        /// <summary>
        /// Configuration used for Application Insights.
        /// </summary>
        TelemetryConfiguration Configuration { get; }

        /// <summary>
        /// Initialize is initialize the telemetry client.
        /// </summary>
        void Initialize(ILogger logger);
    }
}
