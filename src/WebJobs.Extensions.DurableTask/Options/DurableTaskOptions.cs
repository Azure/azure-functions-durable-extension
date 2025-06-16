// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using DurableTask.AzureStorage.Partitioning;
using DurableTask.Core.Settings;
using Microsoft.Azure.WebJobs.Host;
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
        /// The DefaultVersion value provided here will be automatically assigned to every orchestration
        /// instance created by this app, and this value will be available on IDurableOrchestrationContext
        /// passed to each orchestrator function replay invocation of this orchestration instance.
        /// </summary>
        public string DefaultVersion { get; set; }

        /// <summary>
        /// The strategy that will be used for matching versions when running an orchestration. See <see cref="VersioningSettings.VersionMatchStrategy"/> for more information.
        /// </summary>
        public VersioningSettings.VersionMatchStrategy VersionMatchStrategy { get; set; } = VersioningSettings.VersionMatchStrategy.CurrentOrOlder;

        /// <summary>
        /// The strategy that will be used if a versioning failure is detected. See <see cref="VersioningSettings.VersionFailureStrategy"/> for more information.
        /// </summary>
        public VersioningSettings.VersionFailureStrategy VersionFailureStrategy { get; set; } = VersioningSettings.VersionFailureStrategy.Reject;

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
        /// A positive integer configured by the host.
        /// </value>
        public int? MaxConcurrentActivityFunctions { get; set; } = null;

        /// <summary>
        /// Gets or sets the maximum number of orchestrator functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <value>
        /// A positive integer configured by the host.
        /// </value>
        public int? MaxConcurrentOrchestratorFunctions { get; set; } = null;

        /// <summary>
        /// Gets or sets the maximum number of entity functions that can be processed concurrently on a single host instance.
        /// </summary>
        /// <remarks>
        /// Increasing entity function concurrency can result in increased throughput but can
        /// also increase the total CPU and memory usage on a single worker instance.
        /// </remarks>
        /// <value>
        /// A positive integer configured by the host.
        /// </value>
        public int? MaxConcurrentEntityFunctions { get; set; } = null;

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
        /// Gets or sets the maximum number of entity operations that are processed as a single batch.
        /// </summary>
        /// <remarks>
        /// Reducing this number can help to avoid timeouts on consumption plans. If set to 1, batching is disabled, and each operation
        /// message executes and is billed as a separate function invocation.
        /// </remarks>
        /// <value>
        /// A positive integer configured by the host.
        /// </value>
        public int? MaxEntityOperationBatchSize { get; set; } = null;

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
        /// Controls the behavior of <see cref="IDurableOrchestrationClient.RaiseEventAsync(string,string,object)"/> in situations where the specified orchestration
        /// does not exist, or is not in a running state. If set to true, an exception is thrown. If set to false, the event is silently discarded.
        /// </summary>
        /// <remarks>
        /// The default behavior depends on the selected storage provider.
        /// </remarks>
        public bool? ThrowStatusExceptionsOnRaiseEvent { get; set; } = null;

        /// <summary>
        /// If true, takes a lease on the task hub container, allowing for only one app to process messages in a task hub at a time.
        /// </summary>
        public bool UseAppLease { get; set; } = true;

        /// <summary>
        /// If <c>true</c>, the inputs of functions called by the orchestrator will be saved to the orchestration history, making them visible to debugging tools. The default value is <c>false</c>.
        /// </summary>
        public bool StoreInputsInOrchestrationHistory { get; set; } = false;

        /// <summary>
        /// If UseAppLease is true, gets or sets the AppLeaaseOptions used for acquiring the lease to start the application.
        /// </summary>
        public AppLeaseOptions AppLeaseOptions { get; set; } = AppLeaseOptions.DefaultOptions;

        /// <summary>
        /// Option to control the receive message size in bytes of the gRPC client, which is used by Durable Functions C# Isolated and Java (and potentially more languages in the future).
        /// If the server does not respond within this period, the HTTP request will time out.
        /// Defaults to 4,194,304 (4 MB).
        /// The maximum allowable value is <see cref="int.MaxValue"/>, which corresponds to the durable grpc server's receive limit.
        /// </summary>
        public int MaxGrpcMessageSizeInBytes { get; set; } = 4194304;

        /// <summary>
        /// Sets the timeout for the HTTP client used by the gRPC client, which is used by Durable Functions C# Isolated and Java (and potentially more languages in the future).
        /// If the server does not respond within this period, the HTTP request will time out.
        /// Default is 100 seconds.
        /// This settings only applies when .NET 6 or greater is used.
        /// </summary>
        public TimeSpan? GrpcHttpClientTimeout { get; set; } = TimeSpan.FromSeconds(100);

        // Used for mocking the lifecycle notification helper.
        internal HttpMessageHandler NotificationHandler { get; set; }

        // This is just a way for tests to overwrite the webhook url, since there is no easy way
        // to mock the value from ExtensionConfigContext. It should not be used in production code paths.
        internal Func<Uri> WebhookUriProviderOverride { get; set; }

        internal static void ResolveAppSettingOptions(DurableTaskOptions options, INameResolver nameResolver)
        {
            if (options == null)
            {
                throw new InvalidOperationException($"{nameof(options)} must be set before resolving app settings.");
            }

            if (nameResolver == null)
            {
                throw new InvalidOperationException($"{nameof(nameResolver)} must be set before resolving app settings.");
            }

            if (nameResolver.TryResolveWholeString(options.HubName, out string taskHubName))
            {
                // use the resolved task hub name
                options.HubName = taskHubName;
            }
        }

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

        internal void Validate(INameResolver environmentVariableResolver)
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
                throw new InvalidOperationException(
                    "Durable Functions with extendedSessionsEnabled set to 'true' is only supported when using the in-process .NET worker. Please remove the setting or change it to 'false'." +
                    "See https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-perf-and-scale#extended-sessions for more details.");
            }

            this.Notifications.Validate();

            if (this.MaxConcurrentActivityFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentActivityFunctions)} must be a positive integer value.");
            }

            if (this.MaxConcurrentOrchestratorFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentOrchestratorFunctions)} must be a positive integer value.");
            }

            if (this.MaxConcurrentEntityFunctions <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxConcurrentEntityFunctions)} must be a positive integer value.");
            }

            if (this.MaxEntityOperationBatchSize <= 0)
            {
                throw new InvalidOperationException($"{nameof(this.MaxEntityOperationBatchSize)} must be a positive integer value.");
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
