// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using DurableTask.ApplicationInsights;
using DurableTask.Core;
using DurableTask.Core.Settings;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// TelemetryActivator initializes Distributed Tracing. This class only works for netstandard2.0.
    /// </summary>
    public class TelemetryActivator : ITelemetryModule, IAsyncDisposable, IDisposable
    {
        private readonly DurableTaskOptions options;
        private readonly INameResolver nameResolver;
        private TelemetryClient telemetryClient;

        /// <summary>
        /// Constructor for initializing Distributed Tracing.
        /// </summary>
        /// <param name="options">DurableTask options.</param>
        /// <param name="nameResolver">Name resolver used for environment variables.</param>
        public TelemetryActivator(IOptions<DurableTaskOptions> options, INameResolver nameResolver)
        {
            this.options = options.Value;
            this.nameResolver = nameResolver;
        }

        internal IAsyncDisposable TelemetryModule { get; set; }

        internal IAsyncDisposable WebJobsTelemetryModule { get; set; }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            if (this.TelemetryModule != null)
            {
                this.TelemetryModule.DisposeAsync();
            }

            if (this.WebJobsTelemetryModule != null)
            {
                this.WebJobsTelemetryModule.DisposeAsync();
            }

            return default;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Initialize is initialize the telemetry client.
        /// </summary>
        public void Initialize(TelemetryConfiguration configuration)
        {
            if (this.options.Tracing.DistributedTracingEnabled)
            {
                if (this.options.Tracing.Version == Options.DurableDistributedTracingVersion.None)
                {
                    return;
                }

                if (this.options.Tracing.Version == Options.DurableDistributedTracingVersion.V2)
                {
                    DurableTelemetryModule module = new DurableTelemetryModule();
                    module.Initialize(configuration);
                    this.TelemetryModule = module;

                    WebJobsTelemetryModule webJobsModule = new WebJobsTelemetryModule();
                    webJobsModule.Initialize(configuration);
                    this.WebJobsTelemetryModule = webJobsModule;
                }
                else
                {
                    this.SetUpV1DistributedTracing();
                    if (CorrelationSettings.Current.EnableDistributedTracing)
                    {
                        this.SetUpTelemetryClient(configuration);

                        if (CorrelationSettings.Current.EnableDistributedTracing)
                        {
                            this.SetUpTelemetryCallbacks();
                        }
                    }
                }
            }
        }

        private void SetUpV1DistributedTracing()
        {
            DurableTaskOptions durableTaskOptions = this.options;
            CorrelationSettings.Current.EnableDistributedTracing =
                durableTaskOptions.Tracing.DistributedTracingEnabled;
            CorrelationSettings.Current.Protocol =
                durableTaskOptions.Tracing.DistributedTracingProtocol == Protocol.W3CTraceContext.ToString()
                    ? Protocol.W3CTraceContext
                    : Protocol.HttpCorrelationProtocol;
        }

        private void SetUpTelemetryCallbacks()
        {
            var resolvedSiteName = this.nameResolver?.Resolve("WEBSITE_SITE_NAME")?.ToLower() ?? string.Empty;

            CorrelationTraceClient.SetUp(
                (TraceContextBase requestTraceContext) =>
                {
                    requestTraceContext.Stop();

                    var requestTelemetry = requestTraceContext.CreateRequestTelemetry(resolvedSiteName);
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

        private void SetUpTelemetryClient(TelemetryConfiguration telemetryConfiguration)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
        }
    }
}