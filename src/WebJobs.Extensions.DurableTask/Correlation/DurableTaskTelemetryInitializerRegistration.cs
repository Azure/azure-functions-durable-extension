// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    internal sealed class DurableTaskTelemetryInitializerRegistration
    {
        public DurableTaskTelemetryInitializerRegistration(Func<IServiceProvider, ITelemetryInitializer> factory)
        {
            this.Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public Func<IServiceProvider, ITelemetryInitializer> Factory { get; }
    }
}
