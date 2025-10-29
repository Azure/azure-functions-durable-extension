// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Scale.AzureStorage
{
    /// <summary>
    /// The Azure Storage implementation of additional methods not required by IOrchestrationService.
    /// </summary>
    public class AzureStorageDurabilityProvider : DurabilityProvider
    {
        private readonly AzureStorageOrchestrationService serviceClient;
        private readonly IStorageServiceClientProviderFactory clientProviderFactory;
        private readonly string connectionName;
        private readonly JObject storageOptionsJson;
        private readonly ILogger logger;

        private readonly object initLock = new object();

        private DurableTaskMetricsProvider singletonDurableTaskMetricsProvider;

        public AzureStorageDurabilityProvider(
            AzureStorageOrchestrationService service,
            IStorageServiceClientProviderFactory clientProviderFactory,
            string connectionName,
            AzureStorageOptions options,
            ILogger logger)
            : base("Azure Storage", service, service, connectionName)
        {
            this.serviceClient = service;
            this.clientProviderFactory = clientProviderFactory;
            this.connectionName = connectionName;
            this.storageOptionsJson = JObject.FromObject(
                options,
                new JsonSerializer
                {
                    Converters = { new StringEnumConverter() },
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                });
            this.logger = logger;
        }

        /// <summary>
        /// The app setting containing the Azure Storage connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        public override JObject ConfigurationJson => this.storageOptionsJson;

        public override string EventSourceName { get; set; } = "DurableTask-AzureStorage";

        internal DurableTaskMetricsProvider GetMetricsProvider(
            string hubName,
            StorageAccountClientProvider storageAccountClientProvider,
            ILogger logger)
        {
            return new DurableTaskMetricsProvider(hubName, logger, performanceMonitor: null, storageAccountClientProvider);
        }

        /// <inheritdoc/>
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out IScaleMonitor scaleMonitor)
        {
            lock (this.initLock)
            {
                if (this.singletonDurableTaskMetricsProvider == null)
                {
                    // This is only called by the ScaleController, it doesn't run in the Functions Host process.
                    this.singletonDurableTaskMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.clientProviderFactory.GetClientProvider(connectionName),
                        this.logger);
                }

                scaleMonitor = new DurableTaskScaleMonitor(functionId, hubName, this.logger, this.singletonDurableTaskMetricsProvider);
                return true;
            }
        }

        public override bool TryGetTargetScaler(
            string functionId,
            string functionName,
            string hubName,
            string connectionName,
            out ITargetScaler targetScaler)
        {
            lock (this.initLock)
            {
                if (this.singletonDurableTaskMetricsProvider == null)
                {
                    // This is only called by the ScaleController, it doesn't run in the Functions Host process.
                    this.singletonDurableTaskMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.clientProviderFactory.GetClientProvider(connectionName),
                        this.logger);
                }

                targetScaler = new DurableTaskTargetScaler(functionId, this.singletonDurableTaskMetricsProvider, this, this.logger);
                return true;
            }
        }
    }
}
