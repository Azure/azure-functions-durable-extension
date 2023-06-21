﻿// Copyright (c) .NET Foundation. All rights reserved.
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
    public class TelemetryActivator : ITelemetryActivator, IAsyncDisposable
    {
        private readonly DurableTaskOptions options;
        private readonly INameResolver nameResolver;
        private EndToEndTraceHelper endToEndTraceHelper;
        private TelemetryClient telemetryClient;
        private IAsyncDisposable telemetryModule;

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

        /// <summary>
        /// OnSend is an action that enable to hook of sending telemetry.
        /// You can use this property for testing.
        /// </summary>
        public Action<ITelemetry> OnSend { get; set; } = null;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return this.telemetryModule?.DisposeAsync() ?? default;
        }

        /// <summary>
        /// Initialize is initialize the telemetry client.
        /// </summary>
        public void Initialize(ILogger logger)
        {
            if (this.options.Tracing.DistributedTracingEnabled)
            {
                if (this.options.Tracing.Version == Options.DurableDistributedTracingVersion.None)
                {
                    return;
                }

                this.endToEndTraceHelper = new EndToEndTraceHelper(logger, this.options.Tracing.TraceReplayEvents);
                TelemetryConfiguration telemetryConfiguration = this.SetupTelemetryConfiguration();

                if (this.options.Tracing.Version == Options.DurableDistributedTracingVersion.V2)
                {
                    DurableTelemetryModule module = new DurableTelemetryModule();
                    module.Initialize(telemetryConfiguration);
                    this.telemetryModule = module;
                }
                else
                {
                    this.SetUpV1DistributedTracing();
                    if (CorrelationSettings.Current.EnableDistributedTracing)
                    {
                        this.SetUpTelemetryClient(telemetryConfiguration);

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
            this.endToEndTraceHelper.ExtensionInformationalEvent(
                    hubName: this.options.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: "Setting up the telemetry client...",
                    writeToUserLogs: true);

            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        private TelemetryConfiguration SetupTelemetryConfiguration()
        {
            TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();
            if (this.OnSend != null)
            {
                config.TelemetryChannel = new NoOpTelemetryChannel { OnSend = this.OnSend };
            }

            string resolvedInstrumentationKey = this.nameResolver.Resolve("APPINSIGHTS_INSTRUMENTATIONKEY");
            string resolvedConnectionString = this.nameResolver.Resolve("APPLICATIONINSIGHTS_CONNECTION_STRING");

            bool instrumentationKeyProvided = !string.IsNullOrEmpty(resolvedInstrumentationKey);
            bool connectionStringProvided = !string.IsNullOrEmpty(resolvedConnectionString);

            if (instrumentationKeyProvided && connectionStringProvided)
            {
                this.endToEndTraceHelper.ExtensionWarningEvent(
                    hubName: this.options.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: "Both 'APPINSIGHTS_INSTRUMENTATIONKEY' and 'APPLICATIONINSIGHTS_CONNECTION_STRING' are defined in the current environment variables. Please specify one. We recommend specifying 'APPLICATIONINSIGHTS_CONNECTION_STRING'.");
            }

            if (!instrumentationKeyProvided && !connectionStringProvided)
            {
                this.endToEndTraceHelper.ExtensionWarningEvent(
                    hubName: this.options.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: "'APPINSIGHTS_INSTRUMENTATIONKEY' or 'APPLICATIONINSIGHTS_CONNECTION_STRING' were not defined in the current environment variables, but distributed tracing is enabled. Please specify one. We recommend specifying 'APPLICATIONINSIGHTS_CONNECTION_STRING'.");
            }

            if (instrumentationKeyProvided)
            {
                this.endToEndTraceHelper.ExtensionInformationalEvent(
                    hubName: this.options.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: "Reading APPINSIGHTS_INSTRUMENTATIONKEY...",
                    writeToUserLogs: true);

#pragma warning disable CS0618 // Type or member is obsolete
                config.InstrumentationKey = resolvedInstrumentationKey;
#pragma warning restore CS0618 // Type or member is obsolete
            }

            if (connectionStringProvided)
            {
                this.endToEndTraceHelper.ExtensionInformationalEvent(
                    hubName: this.options.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: "Reading APPLICATIONINSIGHTS_CONNECTION_STRING...",
                    writeToUserLogs: true);

                config.ConnectionString = resolvedConnectionString;
            }

            return config;
        }
    }
}