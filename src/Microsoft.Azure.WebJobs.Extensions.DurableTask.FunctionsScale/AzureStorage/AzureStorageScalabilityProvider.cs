// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using DurableTask.AzureStorage;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.FunctionsScale.AzureStorage
{
    /// <summary>
    /// Azure Storage backend implementation of the scalability provider for Durable Functions scaling decisions.
    /// </summary>
    public class AzureStorageScalabilityProvider : ScalabilityProvider
    {
        private readonly StorageAccountClientProvider storageAccountClientProvider;
        private readonly string connectionName;
        private readonly ILogger logger;

        private readonly object initLock = new object();

        private DurableTaskMetricsProvider singletonDurableTaskMetricsProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorageScalabilityProvider"/> class.
        /// </summary>
        /// <param name="storageAccountClientProvider">
        /// Provides Azure Storage clients using resolved configuration, including
        /// connection strings or token-based credentials.</param>
        /// <param name="connectionName">The name of the storage connection used to resolve host configuration.</param>
        /// <param name="logger">The logger instance used for diagnostics and telemetry.</param>
        public AzureStorageScalabilityProvider(
            StorageAccountClientProvider storageAccountClientProvider,
            string connectionName,
            ILogger logger)
            : base("AzureStorage", connectionName)
        {
            this.storageAccountClientProvider = storageAccountClientProvider ?? throw new ArgumentNullException(nameof(storageAccountClientProvider));
            this.connectionName = connectionName;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the app setting containing the Azure Storage connection string.
        /// </summary>
        public override string ConnectionName => this.connectionName;

        /// <inheritdoc/>
        /// Note: ScaleMonitor is not used in prod. Can be cleaned in future.
        public override bool TryGetScaleMonitor(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out IScaleMonitor scaleMonitor)
        {
            lock (this.initLock)
            {
                if (this.singletonDurableTaskMetricsProvider == null)
                {
                    this.singletonDurableTaskMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.storageAccountClientProvider,
                        this.logger);
                }

                scaleMonitor = new DurableTaskScaleMonitor(functionId, hubName, this.logger, this.singletonDurableTaskMetricsProvider);
                return true;
            }
        }

        /// <inheritdoc/>
        public override bool TryGetTargetScaler(
            string functionId,
            string functionName,
            string hubName,
            string targetConnectionName,
            out ITargetScaler targetScaler)
        {
            lock (this.initLock)
            {
                if (this.singletonDurableTaskMetricsProvider == null)
                {
                    this.singletonDurableTaskMetricsProvider = this.GetMetricsProvider(
                        hubName,
                        this.storageAccountClientProvider,
                        this.logger);
                }

                targetScaler = new DurableTaskTargetScaler(functionId, this.singletonDurableTaskMetricsProvider, this, this.logger);
                return true;
            }
        }

        internal DurableTaskMetricsProvider GetMetricsProvider(
            string hubName,
            StorageAccountClientProvider clientProvider,
            ILogger metricsLogger)
        {
            return new DurableTaskMetricsProvider(hubName, metricsLogger, performanceMonitor: null, clientProvider);
        }
    }
}
