// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.Core;
using DurableTask.Core.Settings;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// TelemetryActivator initializes Distributed Tracing. This class only works for netstandard2.0.
    /// </summary>
    public class TelemetryActivator : ITelemetryActivator
    {
        private TelemetryClient telemetryClient;
        private IOptions<DurableTaskOptions> options;

        /// <summary>
        /// Constructor for initializing Distributed Tracing.
        /// </summary>
        /// <param name="options">DurableTask options.</param>
        public TelemetryActivator(IOptions<DurableTaskOptions> options)
        {
            this.options = options;
        }

        /// <summary>
        /// OnSend is an action that enable to hook of sending telemetry.
        /// You can use this property for testing.
        /// </summary>
        public Action<ITelemetry> OnSend { get; set; } = null;

        /// <summary>
        /// Initialize is initialize the telemetry client.
        /// </summary>
        public void Initialize()
        {
            this.SetUpDistributedTracing();
            if (CorrelationSettings.Current.EnableDistributedTracing)
            {
                this.SetUpTelemetryClient();
                this.SetUpTelemetryCallbacks();
            }
        }

        private void SetUpDistributedTracing()
        {
            DurableTaskOptions durableTaskOptions = this.options.Value;
            CorrelationSettings.Current.EnableDistributedTracing =
                durableTaskOptions.Tracing.DistributedTracingEnabled;
            CorrelationSettings.Current.Protocol =
                durableTaskOptions.Tracing.DistributedTracingProtocol == Protocol.W3CTraceContext.ToString()
                    ? Protocol.W3CTraceContext
                    : Protocol.HttpCorrelationProtocol;
        }

        private void SetUpTelemetryCallbacks()
        {
            CorrelationTraceClient.SetUp(
                (TraceContextBase requestTraceContext) =>
                {
                    requestTraceContext.Stop();

                    var requestTelemetry = requestTraceContext.CreateRequestTelemetry();
                    this.telemetryClient.TrackRequest(requestTelemetry);
                },
                (TraceContextBase dependencyTraceContext) =>
                {
                    dependencyTraceContext.Stop();
                    var dependencyTelemetry = dependencyTraceContext.CreateDependencyTelemetry();
                    this.telemetryClient.TrackDependency(dependencyTelemetry);
                },
                (Exception e) =>
                {
                    this.telemetryClient.TrackException(e);
                });
        }

        private void SetUpTelemetryClient()
        {
            TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();
            if (this.OnSend != null)
            {
                config.TelemetryChannel = new NoOpTelemetryChannel { OnSend = this.OnSend };
            }

            var telemetryInitializer = new DurableTaskCorrelationTelemetryInitializer();

            telemetryInitializer.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("127.0.0.1");
            config.TelemetryInitializers.Add(telemetryInitializer);

            config.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");

            this.telemetryClient = new TelemetryClient(config);
        }
    }
}