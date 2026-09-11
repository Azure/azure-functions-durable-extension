// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using Azure.Identity;
using DurableTask.ApplicationInsights;
using DurableTask.Core;
using DurableTask.Core.Settings;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApplicationInsightsTokenCredentialOptions = Microsoft.Azure.WebJobs.Logging.ApplicationInsights.TokenCredentialOptions;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation
{
    /// <summary>
    /// TelemetryActivator initializes Distributed Tracing. This class only works for netstandard2.0.
    /// </summary>
    public class TelemetryActivator : ITelemetryActivator, IAsyncDisposable, IDisposable
    {
        private readonly DurableTaskOptions options;
        private readonly INameResolver nameResolver;
        private readonly TelemetryConfiguration hostTelemetryConfiguration;
        private EndToEndTraceHelper endToEndTraceHelper;
        private TelemetryClient telemetryClient;

        /// <summary>
        /// Constructor for initializing Distributed Tracing.
        /// </summary>
        /// <param name="options">DurableTask options.</param>
        /// <param name="nameResolver">Name resolver used for environment variables.</param>
        public TelemetryActivator(IOptions<DurableTaskOptions> options, INameResolver nameResolver)
            : this(options, nameResolver, hostTelemetryConfiguration: null)
        {
        }

        /// <summary>
        /// Constructor for initializing Distributed Tracing with the host telemetry configuration.
        /// </summary>
        /// <param name="options">DurableTask options.</param>
        /// <param name="nameResolver">Name resolver used for environment variables.</param>
        /// <param name="hostTelemetryConfiguration">Application Insights configuration owned by the host.</param>
        public TelemetryActivator(
            IOptions<DurableTaskOptions> options,
            INameResolver nameResolver,
            TelemetryConfiguration hostTelemetryConfiguration)
        {
            this.options = options.Value;
            this.nameResolver = nameResolver;
            this.hostTelemetryConfiguration = hostTelemetryConfiguration;
        }

        /// <summary>
        /// OnSend is an action that enable to hook of sending telemetry.
        /// You can use this property for testing.
        /// </summary>
        public Action<ITelemetry> OnSend { get; set; } = null;

        internal IAsyncDisposable TelemetryModule { get; set; }

        internal IAsyncDisposable WebJobsTelemetryModule { get; set; }

        /// <summary>
        /// Gets the private configuration Durable builds for its own telemetry. Exposed so tests can
        /// assert which credential was applied without reaching into Application Insights internals.
        /// </summary>
        internal TelemetryConfiguration TelemetryConfiguration { get; private set; }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (this.TelemetryModule != null)
            {
                await this.TelemetryModule.DisposeAsync();
            }

            if (this.WebJobsTelemetryModule != null)
            {
                await this.WebJobsTelemetryModule.DisposeAsync();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.TelemetryModule != null)
            {
                this.TelemetryModule.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            if (this.WebJobsTelemetryModule != null)
            {
                this.WebJobsTelemetryModule.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Initialize is initialize the telemetry client.
        /// </summary>
        public void Initialize(ILogger logger)
        {
            this.endToEndTraceHelper = new EndToEndTraceHelper(logger, this.options.Tracing.TraceReplayEvents);

            if (this.options.Tracing.DistributedTracingEnabled)
            {
                if (this.options.Tracing.Version == Options.DurableDistributedTracingVersion.None)
                {
                    return;
                }

                TelemetryConfiguration telemetryConfiguration = this.SetupTelemetryConfiguration();
                this.TelemetryConfiguration = telemetryConfiguration;

                if (this.options.Tracing.Version == Options.DurableDistributedTracingVersion.V2)
                {
                    DurableTelemetryModule module = new DurableTelemetryModule();
                    module.Initialize(telemetryConfiguration);
                    this.TelemetryModule = module;

                    WebJobsTelemetryModule webJobsModule = new WebJobsTelemetryModule();
                    webJobsModule.Initialize(telemetryConfiguration);
                    this.WebJobsTelemetryModule = webJobsModule;
                }
                else
                {
                    this.EmitDTV2Announcement();

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
            else
            {
                this.EmitDTV2Announcement();
            }
        }

        private void EmitDTV2Announcement()
        {
            this.endToEndTraceHelper.ExtensionWarningAnnouncement(
                "Durable Functions Distributed Tracing V2 is GA now! Learn how to enable the feature by visiting "
                + "aka.ms/durable-distributed-tracing. "
                + "To disable this message, you can configure distributedTracingEnabled to \"true\" and version to \"V2\" or \"None\". Setting it to \"None\" would in effect disable the feature.");
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

            config.TelemetryInitializers.Add(new DurableTaskInstanceIdTelemetryInitializer(this.options.Tracing.IncludeInstanceIdInOperationName));

            string resolvedInstrumentationKey = this.nameResolver.Resolve("APPINSIGHTS_INSTRUMENTATIONKEY");
            string resolvedConnectionString = this.nameResolver.Resolve("APPLICATIONINSIGHTS_CONNECTION_STRING");
            string resolvedAuthenticationString = this.nameResolver.Resolve("APPLICATIONINSIGHTS_AUTHENTICATION_STRING");

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

            if (!string.IsNullOrWhiteSpace(resolvedAuthenticationString))
            {
                try
                {
                    ApplicationInsightsTokenCredentialOptions tokenCredentialOptions =
                        ApplicationInsightsTokenCredentialOptions.ParseAuthenticationString(resolvedAuthenticationString);
                    bool userAssignedIdentity = tokenCredentialOptions.ClientId != null;
                    bool reusedHostChannel = ApplyEntraAuthentication(
                        config,
                        this.hostTelemetryConfiguration,
                        tokenCredentialOptions,
                        preserveExistingChannel: this.OnSend != null);
                    if (this.hostTelemetryConfiguration != null && !reusedHostChannel && this.OnSend == null)
                    {
                        this.LogTracingWarning(
                            "The Application Insights telemetry channel owned by the Functions host could not be read, so Durable distributed tracing created its own managed identity credential. If Durable spans are missing, the host and the function app are likely loading different Azure.Core versions.");
                    }

                    this.endToEndTraceHelper.ExtensionInformationalEvent(
                        hubName: this.options.HubName,
                        functionName: string.Empty,
                        instanceId: string.Empty,
                        message:
                            "Microsoft Entra authentication enabled for Durable distributed tracing using the "
                            + $"{(userAssignedIdentity ? "user-assigned" : "system-assigned")} managed identity.",
                        writeToUserLogs: true);
                }
                catch (AuthenticationException)
                {
                    this.LogTracingWarning(
                        "APPLICATIONINSIGHTS_AUTHENTICATION_STRING is invalid and will not be used for Durable Functions distributed tracing.");
                }
                catch (FormatException)
                {
                    this.LogTracingWarning(
                        "APPLICATIONINSIGHTS_AUTHENTICATION_STRING is invalid and will not be used for Durable Functions distributed tracing.");
                }
                catch (ArgumentException)
                {
                    this.LogTracingWarning(
                        "APPLICATIONINSIGHTS_AUTHENTICATION_STRING could not be applied and will not be used for Durable Functions distributed tracing.");
                }
            }

            return config;
        }

        /// <summary>
        /// Enables Microsoft Entra authenticated ingestion for the private Durable telemetry
        /// configuration.
        /// </summary>
        /// <remarks>
        /// When the Functions host exposes its telemetry configuration, Durable forwards telemetry
        /// to the host's channel, which already holds the host's credential and Entra ingestion
        /// endpoint. Durable therefore never creates an <c>Azure.Core.TokenCredential</c>, which is
        /// important because the Application Insights SDK resolves that type in the host load
        /// context and rejects a credential created in the function app load context.
        /// Outside the Functions host there is no channel to reuse, so Durable falls back to
        /// creating its own managed identity credential.
        /// </remarks>
        /// <param name="durableConfiguration">The private Durable telemetry configuration.</param>
        /// <param name="hostConfiguration">The host telemetry configuration, if available.</param>
        /// <param name="tokenCredentialOptions">The parsed authentication string.</param>
        /// <param name="preserveExistingChannel">
        /// True when the configuration already has a channel that must not be replaced, such as the
        /// test channel installed by <see cref="OnSend"/>.
        /// </param>
        /// <returns>True when the host channel was reused.</returns>
        internal static bool ApplyEntraAuthentication(
            TelemetryConfiguration durableConfiguration,
            TelemetryConfiguration hostConfiguration,
            ApplicationInsightsTokenCredentialOptions tokenCredentialOptions,
            bool preserveExistingChannel = false)
        {
            ITelemetryChannel hostChannel = hostConfiguration?.TelemetryChannel;
            if (hostChannel != null && !preserveExistingChannel)
            {
                durableConfiguration.TelemetryChannel = new HostForwardingTelemetryChannel(hostChannel);
                return true;
            }

            ManagedIdentityId managedIdentityId = tokenCredentialOptions.ClientId != null
                ? ManagedIdentityId.FromUserAssignedClientId(tokenCredentialOptions.ClientId)
                : ManagedIdentityId.SystemAssigned;
            durableConfiguration.SetAzureTokenCredential(new ManagedIdentityCredential(managedIdentityId));
            return false;
        }

        private void LogTracingWarning(string message)
        {
            this.endToEndTraceHelper.ExtensionWarningEvent(
                hubName: this.options.HubName,
                functionName: string.Empty,
                instanceId: string.Empty,
                message: message);
        }
    }
}
