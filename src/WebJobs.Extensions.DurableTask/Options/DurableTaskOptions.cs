// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using DurableTask.AzureStorage.Partitioning;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    /// <summary>
    /// Configuration options for the Durable Task extension.
    /// </summary>
    public class DurableTaskOptions
    {
        internal const string DefaultHubName = "TestHubName";
        private string originalHubName;
        private string resolvedHubName;
        private string defaultHubName;

        /// <summary>
        /// Settings used for Durable HTTP functionality.
        /// </summary>
        public HttpOptions HttpSettings { get; set; } = new HttpOptions();

        /// <summary>
        /// Gets or sets default task hub name to be used by all <see cref="IDurableClient"/>, <see cref="IDurableEntityClient"/>, <see cref="IDurableOrchestrationClient"/>,
        /// <see cref="IDurableOrchestrationContext"/>, and <see cref="IDurableActivityContext"/> instances.
        /// </summary>
        /// <remarks>
        /// A task hub is a logical grouping of storage resources. Alternate task hub names can be used to isolate
        /// multiple Durable Functions applications from each other, even if they are using the same storage backend.
        /// </remarks>
        /// <value>The name of the default task hub.</value>
        public string HubName
        {
            get
            {
                if (this.resolvedHubName == null)
                {
                    // "WEBSITE_SITE_NAME" is an environment variable used in Azure functions infrastructure. When running locally, this can be
                    // specified in local.settings.json file to avoid being defaulted to "TestHubName"
                    this.resolvedHubName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? DefaultHubName;
                    this.defaultHubName = this.resolvedHubName;
                }

                return this.resolvedHubName;
            }

            set
            {
                if (this.originalHubName == null)
                {
                    this.originalHubName = value;
                }

                this.resolvedHubName = value;
            }
        }

        /// <summary>
        /// The section of configuration related to storage providers. If using Azure Storage provider, the schema should match
        /// <see cref="AzureStorageOptions"/>.
        /// </summary>
        public IDictionary<string, object> StorageProvider { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// The section of configuration related to tracing.
        /// </summary>
        public TraceOptions Tracing { get; set; } = new TraceOptions();

        /// <summary>
        /// The section of configuration related to notifications.
        /// </summary>
        public NotificationOptions Notifications { get; set; } = new NotificationOptions();

        /// <summary>
        /// Gets or sets the maximum number of activity functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <remarks>
        /// Increasing activity function concurrent can result in increased throughput but can
        /// also increase the total CPU and memory usage on a single worker instance.
        /// </remarks>
        /// <value>
        /// A positive integer configured by the host. The default value is 10X the number of processors on the current machine.
        /// </value>
        public int MaxConcurrentActivityFunctions { get; set; } = 10 * Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the maximum number of orchestrator functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <value>
        /// A positive integer configured by the host. The default value is 10X the number of processors on the current machine.
        /// </value>
        public int MaxConcurrentOrchestratorFunctions { get; set; } = 10 * Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the base URL for the HTTP APIs managed by this extension.
        /// </summary>
        /// <remarks>
        /// This property is intended for use only by runtime hosts.
        /// </remarks>
        /// <value>
        /// A URL pointing to the hosted function app that responds to status polling requests.
        /// </value>
        public Uri NotificationUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the local RPC endpoint managed by this extension.
        /// </summary>
        /// <remarks>
        /// The local RPC endpoint is intended to allow out-of-process functions to make direct calls into this
        /// extension. This is primarily intended to support instance management APIs used by the durable client
        /// binding. The following values are allowed:
        /// <list type="table">
        ///   <item>
        ///     <term>null</term>
        ///     <description>(Default) The local RPC endpoint is enabled only for non-.NET function apps.</description>
        ///   </item>
        ///   <item>
        ///     <term>true</term>
        ///     <description>A local RPC endpoint will be enabled and listen at <c>http://127.0.0.1:17071/durabletask/</c>.</description>
        ///   </item>
        ///   <item>
        ///     <term>false</term>
        ///     <description>The local RPC endpoint will be disabled.</description>
        ///   </item>
        /// </list>
        /// </remarks>
        public bool? LocalRpcEndpointEnabled { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether to enable extended sessions.
        /// </summary>
        /// <remarks>
        /// <para>Extended sessions can improve the performance of orchestrator functions by allowing them to skip
        /// replays when new messages are received within short periods of time.</para>
        /// <para>Note that orchestrator functions which are extended this way will continue to count against the
        /// <see cref="MaxConcurrentOrchestratorFunctions"/> limit. To avoid starvation, only half of the maximum
        /// number of allowed concurrent orchestrator functions can be concurrently extended at any given time.
        /// The <see cref="ExtendedSessionIdleTimeoutInSeconds"/> property can also be used to control how long an idle
        /// orchestrator function is allowed to be extended.</para>
        /// <para>It is recommended that this property be set to <c>false</c> during development to help
        /// ensure that the orchestrator code correctly obeys the idempotency rules.</para>
        /// </remarks>
        /// <value>
        /// <c>true</c> to enable extended sessions; otherwise <c>false</c>.
        /// </value>
        public bool ExtendedSessionsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the amount of time in seconds before an idle session times out. The default value is 30 seconds.
        /// </summary>
        /// <remarks>
        /// This setting is applicable when <see cref="ExtendedSessionsEnabled"/> is set to <c>true</c>.
        /// </remarks>
        /// <value>
        /// The number of seconds before an idle session times out.
        /// </value>
        public int ExtendedSessionIdleTimeoutInSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum number of orchestration actions. The default value is 100,000.
        /// </summary>
        public int MaxOrchestrationActions { get; set; } = 100000;

        /// <summary>
        ///  States that will override an existing orchestrator when attempting to start a new orchestrator with the same instance Id.
        /// </summary>
        public OverridableStates OverridableExistingInstanceStates { get; set; } = OverridableStates.NonRunningStates;

        /// <summary>
        /// Gets or sets the time window within which entity messages get deduplicated and reordered.
        /// </summary>
        public int EntityMessageReorderWindowInMinutes { get; set; } = 30;

        /// <summary>
        /// Preview setting for gracefully shutting down to prevent WebJob shutdowns from failing
        /// activities or orchestrations.
        /// </summary>
        public bool UseGracefulShutdown { get; set; } = false;

        /// <summary>
        /// Controls whether an uncaught exception inside an entity operation should roll back the effects of the operation.
        /// </summary>
        /// <remarks>
        /// The rollback undoes all internal effects of an operation
        /// (sent signals, and state creation, deletion, or modification).
        /// However, it does not roll back external effects (such as I/O that was performed).
        /// This setting can affect serialization overhead: if true, the entity state is serialized
        /// after each individual operation. If false, the entity state is serialized
        /// only after an entire batch of operations completes.
        /// </remarks>
        public bool RollbackEntityOperationsOnExceptions { get; set; } = true;

        /// <summary>
        /// If true, takes a lease on the task hub container, allowing for only one app to process messages in a task hub at a time.
        /// </summary>
        public bool UseAppLease { get; set; } = true;

        /// <summary>
        /// If UseAppLease is true, gets or sets the AppLeaaseOptions used for acquiring the lease to start the application.
        /// </summary>
        public AppLeaseOptions AppLeaseOptions { get; set; } = AppLeaseOptions.DefaultOptions;

        // Used for mocking the lifecycle notification helper.
        internal HttpMessageHandler NotificationHandler { get; set; }

        /// <summary>
        /// Sets HubName to a value that is considered a default value.
        /// </summary>
        /// <param name="hubName">TaskHub name that is considered the default.</param>
        public void SetDefaultHubName(string hubName)
        {
            this.HubName = hubName;
            this.defaultHubName = hubName;
        }

        internal void TraceConfiguration(EndToEndTraceHelper traceHelper, JObject storageProviderConfig)
        {
            // Clone the options to avoid making changes to the original.
            // We make updates to the clone rather than to JSON to ensure we're updating what we think we're updating.
            DurableTaskOptions clone = JObject.FromObject(this).ToObject<DurableTaskOptions>();

            // Don't trace the notification URL query string since it may contain secrets.
            // This is the only property which we expect to contain secrets. Everything else should be *names*
            // of secrets that are resolved later from environment variables, etc.
            if (clone.NotificationUrl != null)
            {
                clone.NotificationUrl = new Uri(clone.NotificationUrl.GetLeftPart(UriPartial.Path));
            }

            // At this stage the task hub name is expected to have been resolved. However, we want to know
            // what the original value was in addition to the resolved value, so we're updating the JSON
            // blob property to use the original, unresolved value.
            clone.HubName = this.originalHubName;

            // Format the options data as JSON in a way that is friendly for technical humans to read.
            JObject configurationJson = JObject.FromObject(
                clone,
                new JsonSerializer
                {
                    Converters = { new StringEnumConverter() },
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                });

            if (storageProviderConfig.Count != 0)
            {
                configurationJson["storageProvider"] = storageProviderConfig;
            }

            // This won't be exactly the same as what is declared in host.json because any unspecified values
            // will have been initialized with their defaults. We need the Functions runtime to handle tracing
            // of the actual host.json values: https://github.com/Azure/azure-functions-host/issues/5422.
            traceHelper.TraceConfiguration(this.HubName, configurationJson.ToString(Formatting.None));
        }

        internal void Validate(INameResolver environmentVariableResolver, EndToEndTraceHelper traceHelper)
        {
            if (string.IsNullOrEmpty(this.HubName))
            {
                throw new InvalidOperationException($"A non-empty {nameof(this.HubName)} configuration is required.");
            }

            if (IsInNonProductionSlot() && this.IsDefaultHubName())
            {
                throw new InvalidOperationException($"Task Hub name must be specified in host.json when using slots. Specified name must not equal the default HubName ({this.defaultHubName})." +
                    "See documentation on Task Hubs for information on how to set this: https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-task-hubs");
            }

            string runtimeLanguage = environmentVariableResolver.Resolve("FUNCTIONS_WORKER_RUNTIME");
            if (this.ExtendedSessionsEnabled &&
                runtimeLanguage != null && // If we don't know from the environment variable, don't assume customer isn't .NET
                !string.Equals(runtimeLanguage, "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                traceHelper.ExtensionWarningEvent(
                    hubName: this.HubName,
                    functionName: string.Empty,
                    instanceId: string.Empty,
                    message: "Durable Functions does not work with extendedSessions = true for non-.NET languages. This value is being set to false instead. See https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-perf-and-scale#extended-sessions for more details.");
                this.ExtendedSessionsEnabled = false;
            }

            this.Notifications.Validate();

            if (this.MaxConcurrentActivityFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentActivityFunctions)} must be a non-negative integer value.");
            }

            if (this.MaxConcurrentOrchestratorFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentOrchestratorFunctions)} must be a non-negative integer value.");
            }
        }

        internal bool IsDefaultHubName()
        {
            return string.Equals(this.defaultHubName, this.resolvedHubName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInNonProductionSlot()
        {
            string slotName = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");

            // slotName can be null in a test environment
            if (slotName != null && !string.Equals(slotName, "Production", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
