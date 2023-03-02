// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    internal class DurableFunctionsTelemetryModule : ITelemetryModule
    {
        private readonly DurableTaskOptions options;

        public DurableFunctionsTelemetryModule(IOptions<DurableTaskOptions> options, INameResolver nameResolver)
        {
            this.options = options.Value;
        }

        public void Initialize(TelemetryConfiguration configuration)
        {
            if (this.options.Tracing.NewDistributedTracingEnabled)
            {
                new DurableTelemetryModule().Initialize(configuration);
            }
        }
    }
}
